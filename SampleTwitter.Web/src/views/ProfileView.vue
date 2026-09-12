<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Calendar, Loader2, AlertCircle, RefreshCw, MessageSquare } from 'lucide-vue-next';
import TweetCard from '@/components/tweet/TweetCard.vue';
import { getUserPosts } from '@/api/users';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';
import type { PostFeedItemDto } from '@/types/api';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const targetUserId = computed<number>(() => {
  const idParam = route.params.id;
  if (!idParam) {
    return authStore.currentUserId || 0;
  }
  return Number(idParam);
});

const isMyProfile = computed(() => {
  return authStore.isAuthenticated && authStore.currentUserId !== null && Number(authStore.currentUserId) === targetUserId.value;
});

const loading = ref(true);
const error = ref<string | null>(null);
const isNotFound = ref(false);
const posts = ref<PostFeedItemDto[]>([]);
const activeTab = ref<'posts' | 'replies' | 'highlights' | 'media' | 'likes'>('posts');

const profileEmail = computed(() => {
  if (isMyProfile.value && authStore.currentUserEmail) {
    return authStore.currentUserEmail;
  }
  if (posts.value.length > 0) {
    return posts.value[0].author.email;
  }
  return `user${targetUserId.value}@example.com`;
});

const profileDisplayName = computed(() => {
  if (isMyProfile.value && authStore.currentUserEmail) {
    return authStore.currentUserEmail.split('@')[0];
  }
  if (posts.value.length > 0) {
    return posts.value[0].author.email.split('@')[0];
  }
  return `User #${targetUserId.value}`;
});

const joinedText = computed(() => {
  if (isMyProfile.value && authStore.registeredAt) {
    return formatJoinedDate(authStore.registeredAt);
  }
  if (posts.value.length > 0) {
    return formatJoinedDate(posts.value[posts.value.length - 1].createdAt);
  }
  return null;
});

function formatJoinedDate(dateStr?: string | null): string {
  if (!dateStr) return '';
  try {
    const d = new Date(dateStr);
    return `Joined ${d.toLocaleDateString('en-US', { month: 'long', year: 'numeric' })}`;
  } catch {
    return '';
  }
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

function getAvatar(userId: number): string {
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

async function fetchFeed() {
  if (!targetUserId.value || isNaN(targetUserId.value)) {
    error.value = 'Invalid user ID.';
    loading.value = false;
    return;
  }

  loading.value = true;
  error.value = null;
  isNotFound.value = false;

  try {
    const response = await getUserPosts(targetUserId.value);
    posts.value = response.items || [];
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 404) {
      isNotFound.value = true;
      error.value = 'This account does not exist.';
    } else {
      error.value = parsed.detail || parsed.message || 'Failed to load posts.';
    }
  } finally {
    loading.value = false;
  }
}

function handlePostUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = posts.value.find(p => p.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handlePostDeleted(id: number) {
  posts.value = posts.value.filter(p => p.id !== id);
  for (const post of posts.value) {
    if (post.parentPost?.id === id) {
      post.parentPost = undefined;
    }
  }
}

function goBack() {
  if (window.history.length > 1) {
    router.back();
  } else {
    router.push('/');
  }
}

onMounted(() => {
  fetchFeed();
});

watch(
  () => route.params.id,
  () => {
    fetchFeed();
  }
);
</script>

<template>
  <main class="flex flex-col min-h-screen border-r border-neutral-800 w-full max-w-2xl text-white">
    <!-- Top Sticky Header -->
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-20 flex items-center gap-6 px-4 py-2">
      <button
        @click="goBack"
        class="p-2 rounded-full hover:bg-neutral-800 transition-colors"
        title="Back"
      >
        <ArrowLeft class="w-5 h-5 text-white" />
      </button>

      <div class="flex flex-col truncate">
        <h2 class="text-lg font-bold leading-tight truncate">
          {{ isNotFound ? 'Profile' : profileDisplayName }}
        </h2>
        <span v-if="!loading && !isNotFound" class="text-xs text-neutral-500">
          {{ posts.length }} {{ posts.length === 1 ? 'post' : 'posts' }}
        </span>
      </div>
    </div>

    <!-- Error / Not Found State -->
    <div v-if="isNotFound" class="flex flex-col items-center justify-center p-12 text-center my-8">
      <div class="w-20 h-20 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
        <AlertCircle class="w-10 h-10 text-neutral-500" />
      </div>
      <h3 class="text-2xl font-bold mb-2">This account doesn't exist</h3>
      <p class="text-neutral-500 text-sm max-w-sm mb-6">
        Try searching for another. The user you are looking for might have been deleted or does not exist.
      </p>
      <button
        @click="router.push('/')"
        class="bg-sky-500 hover:bg-sky-400 text-white font-bold py-2.5 px-6 rounded-full text-sm transition-colors"
      >
        Go to Home
      </button>
    </div>

    <div v-else>
      <!-- Profile Header Banner & Avatar -->
      <div class="relative">
        <!-- Banner -->
        <div class="h-36 sm:h-48 bg-gradient-to-r from-sky-900 via-neutral-900 to-indigo-950 border-b border-neutral-800"></div>

        <!-- Avatar & Profile Action -->
        <div class="px-4 pb-4">
          <div class="flex justify-between items-end relative -mt-16 sm:-mt-20 mb-4">
            <img
              :src="getAvatar(targetUserId)"
              alt="Profile Avatar"
              class="w-28 h-28 sm:w-36 sm:h-36 rounded-full border-4 border-black object-cover bg-neutral-800 shadow-xl"
            />

            <div class="mt-2">
              <button
                v-if="isMyProfile"
                class="border border-neutral-600 hover:bg-neutral-900 text-white font-bold text-sm px-4 py-1.5 rounded-full transition-colors"
              >
                Edit profile
              </button>
              <button
                v-else
                class="bg-white hover:bg-neutral-200 text-black font-bold text-sm px-5 py-1.5 rounded-full transition-colors"
              >
                Follow
              </button>
            </div>
          </div>

          <!-- User Details -->
          <div class="flex flex-col gap-1">
            <h1 class="text-xl font-extrabold text-white leading-tight">
              {{ profileDisplayName }}
            </h1>
            <span class="text-sm text-neutral-500">
              @{{ profileEmail }}
            </span>
          </div>

          <!-- Bio / Metadata -->
          <div class="flex flex-wrap items-center gap-4 mt-3 text-xs text-neutral-500">
            <div v-if="joinedText" class="flex items-center gap-1.5">
              <Calendar class="w-4 h-4 text-neutral-500" />
              <span>{{ joinedText }}</span>
            </div>
          </div>

          <!-- Following / Followers metrics -->
          <div class="flex items-center gap-4 mt-3 text-sm">
            <div class="hover:underline cursor-pointer">
              <span class="font-bold text-white">0</span>
              <span class="text-neutral-500 ml-1">Following</span>
            </div>
            <div class="hover:underline cursor-pointer">
              <span class="font-bold text-white">0</span>
              <span class="text-neutral-500 ml-1">Followers</span>
            </div>
          </div>
        </div>

        <!-- Profile Tabs -->
        <div class="flex border-b border-neutral-800 text-sm font-bold select-none overflow-x-auto">
          <button
            @click="activeTab = 'posts'"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative',
              activeTab === 'posts' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Posts
            <div
              v-if="activeTab === 'posts'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>

          <button
            @click="activeTab = 'replies'"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative',
              activeTab === 'replies' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Replies
            <div
              v-if="activeTab === 'replies'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>

          <button
            @click="activeTab = 'highlights'"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative',
              activeTab === 'highlights' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Highlights
            <div
              v-if="activeTab === 'highlights'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>

          <button
            @click="activeTab = 'media'"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative',
              activeTab === 'media' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Media
            <div
              v-if="activeTab === 'media'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>

          <button
            @click="activeTab = 'likes'"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative',
              activeTab === 'likes' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Likes
            <div
              v-if="activeTab === 'likes'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>
        </div>
      </div>

      <!-- Feed Content -->
      <div v-if="loading" class="flex justify-center items-center py-16">
        <Loader2 class="w-8 h-8 text-sky-500 animate-spin" />
      </div>

      <div v-else-if="error" class="p-6 text-center">
        <p class="text-sm text-red-400 mb-3">{{ error }}</p>
        <button
          @click="fetchFeed"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors"
        >
          <RefreshCw class="w-3.5 h-3.5" />
          <span>Retry</span>
        </button>
      </div>

      <!-- Empty State -->
      <div v-else-if="posts.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
        <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
          <MessageSquare class="w-8 h-8 text-neutral-600" />
        </div>
        <h3 class="text-xl font-bold mb-1">No posts yet</h3>
        <p class="text-neutral-500 text-sm max-w-xs">
          {{ isMyProfile ? "When you post, your posts and thread replies will show up here." : "When this user posts, their posts will show up here." }}
        </p>
      </div>

      <!-- Posts List -->
      <div v-else class="flex flex-col">
        <div
          v-for="post in posts"
          :key="'feed-post-' + post.id"
          class="flex flex-col border-b border-neutral-800"
        >
          <!-- Self-thread parent post context if present -->
          <div v-if="post.parentPost" class="relative bg-neutral-950/20">
            <div class="px-4 pt-2.5 flex items-center gap-2 text-xs text-neutral-500">
              <span class="inline-block w-2 h-2 rounded-full bg-sky-500/80"></span>
              <span>Thread root</span>
            </div>

            <!-- Parent Post Card -->
            <div class="relative">
              <TweetCard
                :id="post.parentPost.id"
                :user-id="post.parentPost.author.id"
                :author="post.parentPost.author.email.split('@')[0]"
                :handle="'@' + post.parentPost.author.email"
                :avatar="getAvatar(post.parentPost.author.id)"
                :content="post.parentPost.text || ''"
                :image-url="post.parentPost.imageUrl"
                :timestamp="formatDate(post.parentPost.createdAt)"
                :updated-at="post.parentPost.updatedAt"
                :can-edit="false"
                class="!border-b-0 pb-2"
              />
              <!-- Connector line -->
              <div class="absolute left-9 top-14 bottom-0 w-0.5 bg-neutral-700"></div>
            </div>

            <!-- Replying indicator -->
            <div class="px-4 pb-1 pl-16 text-xs text-neutral-500">
              Replying to <span class="text-sky-400">@{{ post.parentPost.author.email }}</span>
            </div>
          </div>

          <!-- Main Post Card -->
          <TweetCard
            :id="post.id"
            :user-id="post.author.id"
            :author="post.author.email.split('@')[0]"
            :handle="'@' + post.author.email"
            :avatar="getAvatar(post.author.id)"
            :content="post.text || ''"
            :image-url="post.imageUrl"
            :timestamp="formatDate(post.createdAt)"
            :updated-at="post.updatedAt"
            :can-edit="isMyProfile || (authStore.currentUserId !== null && Number(authStore.currentUserId) === post.author.id)"
            @updated="handlePostUpdated"
            @delete="handlePostDeleted"
            class="!border-b-0"
          />
        </div>
      </div>
    </div>
  </main>
</template>
