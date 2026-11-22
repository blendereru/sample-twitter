import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import {
  getConversations,
  getMessages,
  markConversationAsRead,
  sendMessage as apiSendMessage,
  startConversation as apiStartConversation,
} from '@/api/conversations';
import { chatConnection } from '@/realtime/chatConnection';
import { useAuthStore } from '@/stores/auth';
import type { ConversationReadDto, ConversationResponse, MessageResponse, TypingDto } from '@/types/api';

export const useChatStore = defineStore('chat', () => {
  const authStore = useAuthStore();

  const conversations = ref<ConversationResponse[]>([]);
  const messagesByConversation = ref<Record<number, MessageResponse[]>>({});
  const hasMoreByConversation = ref<Record<number, boolean>>({});
  const typingByConversation = ref<Record<number, boolean>>({});
  const activeConversationId = ref<number | null>(null);
  const loadingInbox = ref(false);
  const loadingMessages = ref(false);

  const typingTimers = new Map<number, ReturnType<typeof setTimeout>>();
  const lastTypingSentAt = new Map<number, number>();
  let listenersBound = false;
  let nextTempId = -1;

  const totalUnreadCount = computed(() =>
    conversations.value.reduce((sum, conv) => sum + (conv.unreadCount || 0), 0)
  );

  function bindRealtimeListeners() {
    if (listenersBound) return;
    listenersBound = true;

    chatConnection.onMessageReceived(handleIncomingMessage);
    chatConnection.onConversationRead(handleConversationRead);
    chatConnection.onTypingStarted(handleTypingStarted);
    chatConnection.onReconnected(() => {
      void catchUpAfterReconnect();
    });
  }

  async function connect() {
    if (!authStore.isAuthenticated) return;
    bindRealtimeListeners();
    await chatConnection.start();
    await loadConversations();
  }

  async function disconnect() {
    await chatConnection.stop();
    conversations.value = [];
    messagesByConversation.value = {};
    hasMoreByConversation.value = {};
    typingByConversation.value = {};
    activeConversationId.value = null;
    for (const timer of typingTimers.values()) {
      clearTimeout(timer);
    }
    typingTimers.clear();
  }

  async function loadConversations() {
    if (!authStore.isAuthenticated) return;
    loadingInbox.value = true;
    try {
      conversations.value = await getConversations();
    } finally {
      loadingInbox.value = false;
    }
  }

  async function startOrOpenConversation(recipientId: number): Promise<ConversationResponse> {
    const conv = await apiStartConversation(recipientId);
    const existing = conversations.value.find((c) => c.id === conv.id);
    if (!existing) {
      conversations.value.unshift(conv);
    }
    return conv;
  }

  async function openConversation(conversationId: number) {
    activeConversationId.value = conversationId;
    loadingMessages.value = true;
    try {
      if (!conversations.value.some((c) => c.id === conversationId)) {
        await loadConversations();
      }
      const newestFirst = await getMessages(conversationId, { limit: 30 });
      messagesByConversation.value[conversationId] = [...newestFirst].reverse();
      hasMoreByConversation.value[conversationId] = newestFirst.length >= 30;
      await markAsRead(conversationId);
    } finally {
      loadingMessages.value = false;
    }
  }

  function closeConversation(conversationId: number) {
    if (activeConversationId.value === conversationId) {
      activeConversationId.value = null;
    }
  }

  async function loadOlderMessages(conversationId: number) {
    const existing = messagesByConversation.value[conversationId] || [];
    const oldestReal = existing.find((m) => m.id > 0);
    if (!oldestReal) return;

    const olderNewestFirst = await getMessages(conversationId, {
      before: oldestReal.id,
      limit: 30,
    });
    hasMoreByConversation.value[conversationId] = olderNewestFirst.length >= 30;

    if (olderNewestFirst.length === 0) return;

    const existingIds = new Set(existing.map((m) => m.id));
    const toPrepend = [...olderNewestFirst].reverse().filter((m) => !existingIds.has(m.id));
    messagesByConversation.value[conversationId] = [...toPrepend, ...existing];
  }

  async function sendMessage(conversationId: number, rawText: string): Promise<MessageResponse> {
    const trimmed = rawText.trim();
    const tempId = nextTempId--;
    const pendingMessage: MessageResponse = {
      id: tempId,
      conversationId,
      senderId: authStore.currentUserId ?? 0,
      senderUsername: authStore.currentUsername ?? 'You',
      text: trimmed,
      sentAt: new Date().toISOString(),
      pending: true,
    };

    const list = messagesByConversation.value[conversationId] ?? [];
    messagesByConversation.value[conversationId] = [...list, pendingMessage];

    try {
      const saved = await apiSendMessage(conversationId, trimmed);
      replaceOrAppendMessage(conversationId, tempId, saved);
      updateConversationPreview(saved, false);
      return saved;
    } catch (err) {
      messagesByConversation.value[conversationId] = (
        messagesByConversation.value[conversationId] ?? []
      ).filter((m) => m.id !== tempId);
      throw err;
    }
  }

  async function markAsRead(conversationId: number) {
    try {
      await markConversationAsRead(conversationId);
      const conv = conversations.value.find((c) => c.id === conversationId);
      if (conv) {
        conv.unreadCount = 0;
      }
    } catch {
      // Non-fatal if conversation cannot be marked read immediately.
    }
  }

  function notifyTyping(conversationId: number) {
    const now = Date.now();
    const last = lastTypingSentAt.get(conversationId) ?? 0;
    if (now - last < 2000) return;
    lastTypingSentAt.set(conversationId, now);
    void chatConnection.sendTyping(conversationId);
  }

  function replaceOrAppendMessage(
    conversationId: number,
    tempId: number | null,
    message: MessageResponse
  ) {
    const list = messagesByConversation.value[conversationId] ?? [];
    const alreadyExists = list.some((m) => m.id === message.id);

    if (tempId !== null) {
      if (alreadyExists) {
        messagesByConversation.value[conversationId] = list.filter((m) => m.id !== tempId);
      } else {
        messagesByConversation.value[conversationId] = list.map((m) =>
          m.id === tempId ? message : m
        );
      }
      return;
    }

    if (!alreadyExists) {
      messagesByConversation.value[conversationId] = [...list, message];
    }
  }

  function updateConversationPreview(message: MessageResponse, incrementUnread: boolean) {
    const idx = conversations.value.findIndex((c) => c.id === message.conversationId);
    if (idx === -1) {
      void loadConversations();
      return;
    }

    const conv = conversations.value[idx];
    conv.lastMessage = message;
    conv.lastMessageAt = message.sentAt;
    if (incrementUnread) {
      conv.unreadCount = (conv.unreadCount || 0) + 1;
    }

    if (idx > 0) {
      conversations.value.splice(idx, 1);
      conversations.value.unshift(conv);
    }
  }

  function handleIncomingMessage(message: MessageResponse) {
    const isFromOther = message.senderId !== authStore.currentUserId;
    const isViewing = activeConversationId.value === message.conversationId;

    if (messagesByConversation.value[message.conversationId]) {
      replaceOrAppendMessage(message.conversationId, null, message);
    }

    if (typingByConversation.value[message.conversationId]) {
      typingByConversation.value[message.conversationId] = false;
    }

    updateConversationPreview(message, isFromOther && !isViewing);

    if (isFromOther && isViewing) {
      void markAsRead(message.conversationId);
    }
  }

  function handleConversationRead(receipt: ConversationReadDto) {
    const conv = conversations.value.find((c) => c.id === receipt.conversationId);
    if (conv && receipt.readerId !== authStore.currentUserId) {
      conv.otherParticipantLastReadAt = receipt.readAt;
    }

    const list = messagesByConversation.value[receipt.conversationId];
    if (list && receipt.readerId !== authStore.currentUserId) {
      const readTime = new Date(receipt.readAt).getTime();
      for (const msg of list) {
        if (
          msg.senderId !== receipt.readerId &&
          !msg.readAt &&
          new Date(msg.sentAt).getTime() <= readTime
        ) {
          msg.readAt = receipt.readAt;
        }
      }
    }
  }

  function handleTypingStarted(typing: TypingDto) {
    if (typing.userId === authStore.currentUserId) return;
    typingByConversation.value[typing.conversationId] = true;

    const existingTimer = typingTimers.get(typing.conversationId);
    if (existingTimer) clearTimeout(existingTimer);

    const timer = setTimeout(() => {
      typingByConversation.value[typing.conversationId] = false;
      typingTimers.delete(typing.conversationId);
    }, 3000);

    typingTimers.set(typing.conversationId, timer);
  }

  async function catchUpAfterReconnect() {
    await loadConversations();

    const activeId = activeConversationId.value;
    if (!activeId) return;

    const existing = messagesByConversation.value[activeId] ?? [];
    const lastReal = [...existing].reverse().find((m) => m.id > 0);
    if (!lastReal) {
      await openConversation(activeId);
      return;
    }

    try {
      const missed = await getMessages(activeId, { after: lastReal.id, limit: 50 });
      for (const msg of missed) {
        replaceOrAppendMessage(activeId, null, msg);
      }
      if (missed.some((m) => m.senderId !== authStore.currentUserId)) {
        await markAsRead(activeId);
      }
    } catch {
      // Ignore catch-up errors if conversation is no longer accessible.
    }
  }

  return {
    conversations,
    messagesByConversation,
    hasMoreByConversation,
    typingByConversation,
    activeConversationId,
    loadingInbox,
    loadingMessages,
    totalUnreadCount,
    connect,
    disconnect,
    loadConversations,
    startOrOpenConversation,
    openConversation,
    closeConversation,
    loadOlderMessages,
    sendMessage,
    markAsRead,
    notifyTyping,
  };
});