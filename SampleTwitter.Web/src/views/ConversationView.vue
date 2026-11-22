<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Loader2, Send, Check, CheckCheck } from 'lucide-vue-next';
import { useAuthStore } from '@/stores/auth';
import { useChatStore } from '@/stores/chat';
import { parseApiError } from '@/api/client';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const chatStore = useChatStore();

const conversationId = computed(() => Number(route.params.id));
const conversation = computed(() =>
  chatStore.conversations.find((c) => c.id === conversationId.value) ?? null
);
const messages = computed(() => chatStore.messagesByConversation[conversationId.value] ?? []);
const hasMore = computed(() => Boolean(chatStore.hasMoreByConversation[conversationId.value]));
const isOtherTyping = computed(() => Boolean(chatStore.typingByConversation[conversationId.value]));

const messageText = ref('');
const sending = ref(false);
const loadingOlder = ref(false);
const error = ref<string | null>(null);
const sendError = ref<string | null>(null);
const bottomAnchorRef = ref<HTMLElement | null>(null);

const remainingChars = computed(() => 1000 - messageText.value.length);
const canSend = computed(() =>
  messageText.value.trim().length > 0 && remainingChars.value >= 0 && !sending.value
);

function getAvatar(userId: number, imageUrl?: string | null): string {
  if (imageUrl) return imageUrl;
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

function formatMessageTime(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return '';
  }
}

function isMessageReadByOther(sentAt: string): boolean {
  const otherLastRead = conversation.value?.otherParticipantLastReadAt;
  if (!otherLastRead) return false;
  return new Date(otherLastRead).getTime() >= new Date(sentAt).getTime();
}

async function scrollToBottom() {
  await nextTick();
  bottomAnchorRef.value?.scrollIntoView({ behavior: 'smooth' });
}

async function loadThread(id: number) {
  if (!authStore.isAuthenticated) {
    router.replace('/login');
    return;
  }
  if (!id || Number.isNaN(id)) {
    error.value = 'Invalid conversation.';
    return;
  }

  error.value = null;
  try {
    await chatStore.openConversation(id);
    await scrollToBottom();
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    error.value = parsed.detail || parsed.message || 'Failed to load conversation.';
  }
}

async function handleLoadOlder() {
  if (loadingOlder.value) return;
  loadingOlder.value = true;
  try {
    await chatStore.loadOlderMessages(conversationId.value);
  } finally {
    loadingOlder.value = false;
  }
}

function handleInput() {
  if (messageText.value.trim().length > 0) {
    chatStore.notifyTyping(conversationId.value);
  }
}

async function handleSend() {
  if (!canSend.value) return;
  const textToSend = messageText.value;
  messageText.value = '';
  sending.value = true;
  sendError.value = null;

  try {
    await chatStore.sendMessage(conversationId.value, textToSend);
    await scrollToBottom();
  } catch (err: unknown) {
    messageText.value = textToSend;
    const parsed = parseApiError(err);
    sendError.value = parsed.detail || parsed.message || 'Failed to send message.';
  } finally {
    sending.value = false;
  }
}

watch(
  () => messages.value.length,
  (newLen, oldLen) => {
    if (newLen > oldLen && !loadingOlder.value) {
      void scrollToBottom();
    }
  }
);

watch(
  conversationId,
  (newId, oldId) => {
    if (oldId) {
      chatStore.closeConversation(oldId);
    }
    void loadThread(newId);
  }
);

onMounted(() => {
  void loadThread(conversationId.value);
});

onUnmounted(() => {
  chatStore.closeConversation(conversationId.value);
});
</script>

<template>
  <main class="flex flex-col h-screen border-r border-neutral-800 w-full max-w-2xl text-white">
    <!-- Header -->
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-20 flex items-center gap-3 px-4 py-3 shrink-0">
      <button
        type="button"
        @click="router.push('/messages')"
        class="p-2 rounded-full hover:bg-neutral-800 transition-colors cursor-pointer"
        title="Back to messages"
      >
        <ArrowLeft class="w-5 h-5 text-white" />
      </button>

      <template v-if="conversation">
        <img
          :src="getAvatar(conversation.otherParticipant.id, conversation.otherParticipant.imageUrl)"
          @click="router.push(`/profile/${conversation.otherParticipant.id}`)"
          alt="Avatar"
          class="w-9 h-9 rounded-full object-cover bg-neutral-800 cursor-pointer"
        />
        <div class="flex flex-col min-w-0">
          <span
            @click="router.push(`/profile/${conversation.otherParticipant.id}`)"
            class="font-bold text-white leading-tight truncate hover:underline cursor-pointer"
          >
            {{ conversation.otherParticipant.username }}
          </span>
          <span class="text-xs text-neutral-500 truncate">
            @{{ conversation.otherParticipant.username }}
          </span>
        </div>
      </template>
      <h2 v-else class="text-lg font-bold">Conversation</h2>
    </div>

    <!-- Error / Loading -->
    <div v-if="chatStore.loadingMessages && messages.length === 0" class="flex-1 flex justify-center items-center">
      <Loader2 class="w-8 h-8 text-sky-500 animate-spin" />
    </div>

    <div v-else-if="error" class="flex-1 flex flex-col items-center justify-center p-8 text-center">
      <p class="text-sm text-red-400 mb-4">{{ error }}</p>
      <button
        type="button"
        @click="router.push('/messages')"
        class="px-4 py-2 rounded-full bg-neutral-800 hover:bg-neutral-700 text-sm font-semibold cursor-pointer"
      >
        Back to Inbox
      </button>
    </div>

    <!-- Messages Scroll Area -->
    <div v-else class="flex-1 overflow-y-auto px-4 py-4 flex flex-col gap-2.5">
      <div v-if="hasMore" class="flex justify-center pb-2">
        <button
          type="button"
          @click="handleLoadOlder"
          :disabled="loadingOlder"
          class="px-4 py-1.5 rounded-full border border-neutral-700 hover:bg-neutral-900 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer disabled:opacity-50 flex items-center gap-1.5"
        >
          <Loader2 v-if="loadingOlder" class="w-3.5 h-3.5 animate-spin" />
          <span>Load older messages</span>
        </button>
      </div>

      <div v-if="messages.length === 0" class="flex-1 flex items-center justify-center text-neutral-500 text-sm">
        Say hello to start the conversation.
      </div>

      <div
        v-for="msg in messages"
        :key="msg.id"
        :class="[
          'flex flex-col max-w-[78%]',
          msg.senderId === authStore.currentUserId ? 'self-end items-end' : 'self-start items-start'
        ]"
      >
        <div
          :class="[
            'px-3.5 py-2.5 rounded-2xl text-sm leading-relaxed whitespace-pre-wrap break-words',
            msg.senderId === authStore.currentUserId
              ? 'bg-sky-500 text-white rounded-br-sm'
              : 'bg-neutral-800 text-neutral-100 rounded-bl-sm',
            msg.pending ? 'opacity-70' : ''
          ]"
        >
          {{ msg.text }}
        </div>
        <div class="flex items-center gap-1 mt-0.5 px-1 text-[11px] text-neutral-500">
          <span>{{ msg.pending ? 'Sending...' : formatMessageTime(msg.sentAt) }}</span>
          <template v-if="msg.senderId === authStore.currentUserId && !msg.pending">
            <CheckCheck v-if="isMessageReadByOther(msg.sentAt)" class="w-3.5 h-3.5 text-sky-400" />
            <Check v-else class="w-3.5 h-3.5 text-neutral-500" />
          </template>
        </div>
      </div>

      <div v-if="isOtherTyping" class="self-start text-xs text-sky-400 italic px-2 py-1">
        {{ conversation?.otherParticipant.username || 'User' }} is typing...
      </div>

      <div ref="bottomAnchorRef"></div>
    </div>

    <!-- Composer -->
    <div class="border-t border-neutral-800 p-3 bg-black shrink-0">
      <div v-if="sendError" class="text-xs text-red-400 mb-2 px-1">
        {{ sendError }}
      </div>
      <form @submit.prevent="handleSend" class="flex items-end gap-2">
        <textarea
          v-model="messageText"
          @input="handleInput"
          @keydown.enter.exact.prevent="handleSend"
          rows="1"
          placeholder="Start a new message"
          class="flex-1 bg-neutral-900 border border-neutral-800 rounded-2xl px-4 py-2.5 text-sm text-white placeholder-neutral-500 resize-none focus:outline-none focus:border-sky-500 max-h-32"
        ></textarea>

        <button
          type="submit"
          :disabled="!canSend"
          class="p-2.5 rounded-full bg-sky-500 hover:bg-sky-400 text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer shrink-0"
          title="Send"
        >
          <Loader2 v-if="sending" class="w-4 h-4 animate-spin" />
          <Send v-else class="w-4 h-4" />
        </button>
      </form>
      <div v-if="messageText.length > 900" class="text-right text-xs mt-1 pr-12" :class="remainingChars < 0 ? 'text-red-500 font-bold' : 'text-neutral-500'">
        {{ remainingChars }}
      </div>
    </div>
  </main>
</template>
