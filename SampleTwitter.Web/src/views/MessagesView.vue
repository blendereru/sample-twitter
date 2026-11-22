<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { Mail, Loader2, RefreshCw } from 'lucide-vue-next';
import { useAuthStore } from '@/stores/auth';
import { useChatStore } from '@/stores/chat';
import { parseApiError } from '@/api/client';

const router = useRouter();
const authStore = useAuthStore();
const chatStore = useChatStore();

const error = ref<string | null>(null);

function getAvatar(userId: number, imageUrl?: string | null): string {
  if (imageUrl) return imageUrl;
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

function formatDate(dateStr?: string | null): string {
  if (!dateStr) return '';
  try {
    const date = new Date(dateStr);
    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);
    if (diffSec < 60) return 'Just now';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `${diffHours}h`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return `${diffDays}d`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  } catch {
    return dateStr;
  }
}

async function refresh() {
  if (!authStore.isAuthenticated) {
    router.replace('/login');
    return;
  }
  error.value = null;
  try {
    await chatStore.loadConversations();
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    error.value = parsed.detail || parsed.message || 'Failed to load conversations.';
  }
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <main class="flex flex-col min-h-screen border-r border-neutral-800 w-full max-w-2xl text-white">
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-20 flex items-center justify-between px-4 py-3">
      <h2 class="text-xl font-bold leading-tight">Messages</h2>
      <button
        type="button"
        @click="refresh"
        :disabled="chatStore.loadingInbox"
        class="p-2 rounded-full hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer disabled:opacity-50"
        title="Refresh messages"
      >
        <RefreshCw :class="['w-4 h-4', chatStore.loadingInbox ? 'animate-spin' : '']" />
      </button>
    </div>

    <div v-if="chatStore.loadingInbox && chatStore.conversations.length === 0" class="flex justify-center items-center py-20">
      <Loader2 class="w-8 h-8 text-sky-500 animate-spin" />
    </div>

    <div v-else-if="error" class="p-8 text-center">
      <p class="text-sm text-red-400 mb-3">{{ error }}</p>
      <button
        type="button"
        @click="refresh"
        class="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
      >
        <RefreshCw class="w-3.5 h-3.5" />
        <span>Retry</span>
      </button>
    </div>

    <div v-else-if="chatStore.conversations.length === 0" class="flex flex-col items-center justify-center p-12 text-center my-8">
      <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
        <Mail class="w-8 h-8 text-neutral-600" />
      </div>
      <h3 class="text-2xl font-extrabold mb-1">Welcome to your inbox!</h3>
      <p class="text-neutral-500 text-sm max-w-xs">
        Visit any user's profile and click Message to start a private conversation.
      </p>
    </div>

    <div v-else class="flex flex-col divide-y divide-neutral-800">
      <button
        v-for="conv in chatStore.conversations"
        :key="conv.id"
        type="button"
        @click="router.push(`/messages/${conv.id}`)"
        class="w-full text-left px-4 py-3.5 hover:bg-neutral-900/60 transition-colors flex items-center gap-3 cursor-pointer"
      >
        <img
          :src="getAvatar(conv.otherParticipant.id, conv.otherParticipant.imageUrl)"
          alt="Avatar"
          class="w-11 h-11 rounded-full object-cover shrink-0 bg-neutral-800"
        />

        <div class="flex flex-col flex-1 min-w-0">
          <div class="flex items-center justify-between gap-2">
            <div class="flex items-center gap-1.5 truncate">
              <span class="font-bold text-white truncate">{{ conv.otherParticipant.username }}</span>
              <span class="text-xs text-neutral-500 truncate">@{{ conv.otherParticipant.username }}</span>
            </div>
            <span class="text-xs text-neutral-500 shrink-0">
              {{ formatDate(conv.lastMessageAt ?? conv.createdAt) }}
            </span>
          </div>

          <div class="flex items-center justify-between gap-2 mt-0.5">
            <p
              v-if="chatStore.typingByConversation[conv.id]"
              class="text-sm text-sky-400 italic truncate"
            >
              Typing...
            </p>
            <p
              v-else
              :class="[
                'text-sm truncate',
                conv.unreadCount > 0 ? 'text-white font-semibold' : 'text-neutral-500'
              ]"
            >
              {{ conv.lastMessage ? conv.lastMessage.text : 'No messages yet' }}
            </p>

            <span
              v-if="conv.unreadCount > 0"
              class="min-w-5 h-5 px-1.5 rounded-full bg-sky-500 text-white text-xs font-bold flex items-center justify-center shrink-0"
            >
              {{ conv.unreadCount }}
            </span>
          </div>
        </div>
      </button>
    </div>
  </main>
</template>
