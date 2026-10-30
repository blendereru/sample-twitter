<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { ArrowLeft, Bookmark, Loader2, RefreshCw } from 'lucide-vue-next';
import TweetCard from '@/components/tweet/TweetCard.vue';
import { getUserBookmarks } from '@/api/users';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';
import type { PostResponse, RepostResponse, BookmarkResponse } from '@/types/api';

const router = useRouter();
const authStore = useAuthStore();

const loading = ref(true);
const error = ref<string | null>(null);
const bookmarks = ref<PostResponse[]>([]);

function getAvatar(userId: number): string {
  if (authStore.currentUserId && Number(authStore.currentUserId) === userId && authStore.currentUserImageUrl) {
    return authStore.currentUserImageUrl;
  }
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

function getDisplayName(post: PostResponse): string {
  if (authStore.currentUserId && Number(authStore.currentUserId) === post.author.id && authStore.currentUsername) {
    return authStore.currentUsername;
  }
  return post.author.email.split('@')[0];
}

function formatDate(dateStr: string): string {
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

async function fetchBookmarks() {
  if (!authStore.isAuthenticated) {
    router.replace('/login');
    return;
  }

  loading.value = true;
  error.value = null;

  try {
    const data = await getUserBookmarks();
    bookmarks.value = data || [];
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 401) {
      authStore.clearAuth();
      router.replace('/login');
      return;
    }
    error.value = parsed.detail || parsed.message || 'Failed to load bookmarks.';
  } finally {
    loading.value = false;
  }
}

function handlePostUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = bookmarks.value.find(b => b.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handlePostDeleted(id: number) {
  bookmarks.value = bookmarks.value.filter(b => b.id !== id);
}

function handlePostReposted(response: RepostResponse) {
  const item = bookmarks.value.find(b => b.id === response.postId);
  if (item) {
    item.repostCount = (item.repostCount || 0) + 1;
    item.isReposted = true;
  }
}

function handlePostUndoReposted(postId: number) {
  const item = bookmarks.value.find(b => b.id === postId);
  if (item) {
    item.repostCount = Math.max(0, (item.repostCount || 0) - 1);
    item.isReposted = false;
  }
}

function handlePostBookmarked(response: BookmarkResponse) {
  const item = bookmarks.value.find(b => b.id === response.postId);
  if (item) {
    item.isBookmarked = true;
  }
}

function handlePostUndoBookmarked(postId: number) {
  bookmarks.value = bookmarks.value.filter(b => b.id !== postId);
}

function goBack() {
  if (window.history.length > 1) {
    router.back();
  } else {
    router.push('/');
  }
}

onMounted(() => {
  fetchBookmarks();
});
</script>

<template>
  <main class="flex flex-col min-h-screen border-r border-neutral-800 w-full max-w-2xl text-white">
    <!-- Sticky Header -->
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-20 flex items-center justify-between px-4 py-2">
      <div class="flex items-center gap-6 min-w-0">
        <button
          @click="goBack"
          class="p-2 rounded-full hover:bg-neutral-800 transition-colors cursor-pointer"
          title="Back"
        >
          <ArrowLeft class="w-5 h-5 text-white" />
        </button>

        <div class="flex flex-col truncate">
          <h2 class="text-lg font-bold leading-tight truncate">Bookmarks</h2>
          <span v-if="authStore.currentUserEmail" class="text-xs text-neutral-500 truncate">
            @{{ authStore.currentUserEmail }}
          </span>
        </div>
      </div>

      <button
        v-if="!loading"
        @click="fetchBookmarks"
        class="p-2 rounded-full hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer"
        title="Refresh bookmarks"
      >
        <RefreshCw class="w-4 h-4" />
      </button>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center items-center py-20">
      <Loader2 class="w-8 h-8 text-sky-500 animate-spin" />
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="p-8 text-center">
      <p class="text-sm text-red-400 mb-3">{{ error }}</p>
      <button
        @click="fetchBookmarks"
        class="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
      >
        <RefreshCw class="w-3.5 h-3.5" />
        <span>Retry</span>
      </button>
    </div>

    <!-- Empty Bookmarks State -->
    <div v-else-if="bookmarks.length === 0" class="flex flex-col items-center justify-center p-12 text-center my-8">
      <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
        <Bookmark class="w-8 h-8 text-neutral-600" />
      </div>
      <h3 class="text-2xl font-extrabold mb-1">Save posts for later</h3>
      <p class="text-neutral-500 text-sm max-w-xs">
        Bookmark posts to easily find them again in the future.
      </p>
    </div>

    <!-- Bookmarks Feed -->
    <div v-else class="flex flex-col">
      <TweetCard
        v-for="post in bookmarks"
        :key="post.id"
        :id="post.id"
        :user-id="post.author.id"
        :author="getDisplayName(post)"
        :handle="'@' + post.author.email"
        :avatar="getAvatar(post.author.id)"
        :content="post.text || ''"
        :image-url="post.imageUrl"
        :timestamp="formatDate(post.createdAt)"
        :updated-at="post.updatedAt"
        :retweets="post.repostCount"
        :replies="post.replyCount"
        :reply-id="post.replyId"
        :reply-to-author="post.replyToAuthor"
        :can-edit="authStore.isAuthenticated && Number(authStore.currentUserId) === post.author.id"
        :is-reposted="Boolean(post.isReposted)"
        :is-bookmarked="post.isBookmarked ?? true"
        @updated="handlePostUpdated"
        @delete="handlePostDeleted"
        @reposted="handlePostReposted"
        @undo-repost="handlePostUndoReposted"
        @bookmarked="handlePostBookmarked"
        @undo-bookmark="handlePostUndoBookmarked"
      />
    </div>
  </main>
</template>