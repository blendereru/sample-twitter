<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Calendar, Loader2, AlertCircle, RefreshCw, MessageSquare, Repeat2 } from 'lucide-vue-next';
import TweetCard from '@/components/tweet/TweetCard.vue';
import EditProfileModal from '@/components/profile/EditProfileModal.vue';
import ImageLightbox from '@/components/common/ImageLightbox.vue';
import { getUser, getUserPosts, getUserReplies, getUserReposts, followUser, unfollowUser } from '@/api/users';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';
import type { PostFeedItemDto, ReplyFeedItemDto, PostResponse, PostAuthorDto, RepostResponse, BookmarkResponse, UserResponse } from '@/types/api';

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
const userInfo = ref<UserResponse | null>(null);
const showEditModal = ref(false);
const posts = ref<PostFeedItemDto[]>([]);
const replies = ref<ReplyFeedItemDto[]>([]);
const reposts = ref<PostResponse[]>([]);
const postsLoaded = ref(false);
const repliesLoaded = ref(false);
const repostsLoaded = ref(false);
const profileAuthor = ref<PostAuthorDto | null>(null);
const activeTab = ref<'posts' | 'replies' | 'reposts' | 'highlights' | 'media' | 'likes'>('posts');

const profileEmail = computed(() => {
  if (userInfo.value?.email) {
    return userInfo.value.email;
  }
  if (profileAuthor.value?.email) {
    return profileAuthor.value.email;
  }
  if (isMyProfile.value && authStore.currentUserEmail) {
    return authStore.currentUserEmail;
  }
  return `user${targetUserId.value}@example.com`;
});

const profileDisplayName = computed(() => {
  if (userInfo.value?.username) {
    return userInfo.value.username;
  }
  if (isMyProfile.value && authStore.currentUsername) {
    return authStore.currentUsername;
  }
  return profileEmail.value.split('@')[0];
});

const profileAvatar = computed(() => {
  if (userInfo.value?.imageUrl) {
    return userInfo.value.imageUrl;
  }
  if (isMyProfile.value && authStore.currentUserImageUrl) {
    return authStore.currentUserImageUrl;
  }
  return getAvatar(targetUserId.value);
});

const joinedText = computed(() => {
  if (userInfo.value?.registeredAt) {
    return formatJoinedDate(userInfo.value.registeredAt);
  }
  if (isMyProfile.value && authStore.registeredAt) {
    return formatJoinedDate(authStore.registeredAt);
  }
  if (posts.value.length > 0) {
    return formatJoinedDate(posts.value[posts.value.length - 1].createdAt);
  }
  if (replies.value.length > 0) {
    return formatJoinedDate(replies.value[replies.value.length - 1].createdAt);
  }
  return null;
});

const headerCountText = computed(() => {
  if (activeTab.value === 'replies') {
    const count = replies.value.length;
    return `${count} ${count === 1 ? 'reply' : 'replies'}`;
  }
  if (activeTab.value === 'reposts') {
    const count = reposts.value.length;
    return `${count} ${count === 1 ? 'repost' : 'reposts'}`;
  }
  const count = posts.value.length;
  return `${count} ${count === 1 ? 'post' : 'posts'}`;
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
  if (userId === targetUserId.value && userInfo.value?.imageUrl) {
    return userInfo.value.imageUrl;
  }
  if (authStore.currentUserId && Number(authStore.currentUserId) === userId && authStore.currentUserImageUrl) {
    return authStore.currentUserImageUrl;
  }
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

function getAuthorDisplayName(author: PostAuthorDto): string {
  if (author.id === targetUserId.value && userInfo.value?.username) {
    return userInfo.value.username;
  }
  if (authStore.currentUserId && Number(authStore.currentUserId) === author.id && authStore.currentUsername) {
    return authStore.currentUsername;
  }
  return author.email.split('@')[0];
}

const isFollowLoading = ref(false);
const followHover = ref(false);

async function handleToggleFollow() {
  if (isFollowLoading.value) return;

  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }

  if (!userInfo.value) return;

  isFollowLoading.value = true;
  const currentlyFollowing = Boolean(userInfo.value.isFollowing);

  try {
    if (currentlyFollowing) {
      await unfollowUser(targetUserId.value);
      userInfo.value = {
        ...userInfo.value,
        isFollowing: false,
        followersCount: Math.max(0, (userInfo.value.followersCount ?? 1) - 1),
      };
    } else {
      await followUser(targetUserId.value);
      userInfo.value = {
        ...userInfo.value,
        isFollowing: true,
        followersCount: (userInfo.value.followersCount ?? 0) + 1,
      };
    }
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (!currentlyFollowing && parsed.status === 409) {
      userInfo.value = {
        ...userInfo.value,
        isFollowing: true,
      };
    } else if (currentlyFollowing && parsed.status === 404) {
      userInfo.value = {
        ...userInfo.value,
        isFollowing: false,
      };
    }
  } finally {
    isFollowLoading.value = false;
  }
}

function handleProfileUpdated(payload: { username?: string | null; imageUrl?: string | null; updatedAt?: string | null }) {
  if (userInfo.value) {
    userInfo.value = {
      ...userInfo.value,
      username: payload.username ?? null,
      imageUrl: payload.imageUrl ?? null,
      updatedAt: payload.updatedAt ?? new Date().toISOString(),
    };
  }
}

function getActiveFeedTab(): 'posts' | 'replies' | 'reposts' {
  if (activeTab.value === 'replies') return 'replies';
  if (activeTab.value === 'reposts') return 'reposts';
  return 'posts';
}

async function fetchFeed(tab: 'posts' | 'replies' | 'reposts' = getActiveFeedTab(), options: { silent?: boolean } = {}) {
  if (!targetUserId.value || isNaN(targetUserId.value)) {
    error.value = 'Invalid user ID.';
    loading.value = false;
    return;
  }

  if (!options.silent) {
    loading.value = true;
  }
  error.value = null;
  isNotFound.value = false;

  try {
    if (!userInfo.value || userInfo.value.id !== targetUserId.value) {
      userInfo.value = await getUser(targetUserId.value);
    }

    if (tab === 'replies') {
      const response = await getUserReplies(targetUserId.value);
      replies.value = response.items || [];
      repliesLoaded.value = true;
      if (!profileAuthor.value) {
        profileAuthor.value = response.author || null;
      }
    } else if (tab === 'reposts') {
      const response = await getUserReposts(targetUserId.value);
      reposts.value = response || [];
      repostsLoaded.value = true;
    } else {
      const response = await getUserPosts(targetUserId.value);
      posts.value = response.items || [];
      postsLoaded.value = true;
      if (!profileAuthor.value) {
        profileAuthor.value = response.author || null;
      }
    }
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 404) {
      isNotFound.value = true;
      error.value = 'This account does not exist.';
    } else {
      error.value = parsed.detail || parsed.message || `Failed to load ${tab}.`;
    }
  } finally {
    if (!options.silent) {
      loading.value = false;
    }
  }
}

function switchTab(tab: 'posts' | 'replies' | 'reposts' | 'highlights' | 'media' | 'likes') {
  activeTab.value = tab;
  if (tab === 'posts' && !postsLoaded.value) {
    fetchFeed('posts');
  } else if (tab === 'replies' && !repliesLoaded.value) {
    fetchFeed('replies');
  } else if (tab === 'reposts' && !repostsLoaded.value) {
    fetchFeed('reposts');
  }
}

const myRepostedPostIds = ref<Set<number>>(new Set());
const myBookmarkedPostIds = ref<Set<number>>(new Set());

function handlePostBookmarked(response: BookmarkResponse) {
  myBookmarkedPostIds.value.add(response.postId);
  for (const p of posts.value) {
    if (p.id === response.postId) p.isBookmarked = true;
  }
  for (const r of replies.value) {
    if (r.id === response.postId) r.isBookmarked = true;
  }
  for (const rep of reposts.value) {
    if (rep.id === response.postId) rep.isBookmarked = true;
  }
}

function handlePostUndoBookmarked(postId: number) {
  myBookmarkedPostIds.value.delete(postId);
  for (const p of posts.value) {
    if (p.id === postId) p.isBookmarked = false;
  }
  for (const r of replies.value) {
    if (r.id === postId) r.isBookmarked = false;
  }
  for (const rep of reposts.value) {
    if (rep.id === postId) rep.isBookmarked = false;
  }
}

function handlePostUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = posts.value.find(p => p.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
  const repostItem = reposts.value.find(r => r.id === updated.id);
  if (repostItem) {
    repostItem.text = updated.text;
    repostItem.imageUrl = updated.imageUrl;
    repostItem.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handlePostDeleted(id: number) {
  posts.value = posts.value.filter(p => p.id !== id);
  reposts.value = reposts.value.filter(r => r.id !== id);
}

function handleReplyUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = replies.value.find(r => r.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
  const repostItem = reposts.value.find(r => r.id === updated.id);
  if (repostItem) {
    repostItem.text = updated.text;
    repostItem.imageUrl = updated.imageUrl;
    repostItem.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handleReplyDeleted(id: number) {
  replies.value = replies.value.filter(r => r.id !== id);
  reposts.value = reposts.value.filter(r => r.id !== id);
}

function handlePostReposted(response: RepostResponse) {
  myRepostedPostIds.value.add(response.postId);

  for (const p of posts.value) {
    if (p.id === response.postId) {
      p.repostCount = (p.repostCount || 0) + 1;
      p.isReposted = true;
    }
  }
  for (const r of replies.value) {
    if (r.id === response.postId) {
      r.repostCount = (r.repostCount || 0) + 1;
      r.isReposted = true;
    }
  }
  for (const rep of reposts.value) {
    if (rep.id === response.postId) {
      rep.repostCount = (rep.repostCount || 0) + 1;
      rep.isReposted = true;
    }
  }
  if (isMyProfile.value) {
    repostsLoaded.value = false;
  }
}

function handlePostUndoReposted(postId: number) {
  myRepostedPostIds.value.delete(postId);

  for (const p of posts.value) {
    if (p.id === postId) {
      p.repostCount = Math.max(0, (p.repostCount || 0) - 1);
      p.isReposted = false;
    }
  }
  for (const r of replies.value) {
    if (r.id === postId) {
      r.repostCount = Math.max(0, (r.repostCount || 0) - 1);
      r.isReposted = false;
    }
  }
  if (isMyProfile.value) {
    reposts.value = reposts.value.filter(rep => rep.id !== postId);
  } else {
    for (const rep of reposts.value) {
      if (rep.id === postId) {
        rep.repostCount = Math.max(0, (rep.repostCount || 0) - 1);
        rep.isReposted = false;
      }
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
  fetchFeed('posts');
});

watch(
  () => route.params.id,
  () => {
    userInfo.value = null;
    postsLoaded.value = false;
    repliesLoaded.value = false;
    repostsLoaded.value = false;
    posts.value = [];
    replies.value = [];
    reposts.value = [];
    fetchFeed(getActiveFeedTab());
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
          {{ headerCountText }}
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
            <ImageLightbox
              :src="profileAvatar"
              alt="Profile Avatar"
              img-class="w-28 h-28 sm:w-36 sm:h-36 rounded-full border-4 border-black object-cover bg-neutral-800 shadow-xl"
            />

            <div class="mt-2">
              <button
                v-if="isMyProfile"
                @click="showEditModal = true"
                class="border border-neutral-600 hover:bg-neutral-900 text-white font-bold text-sm px-4 py-1.5 rounded-full transition-colors cursor-pointer"
              >
                Edit profile
              </button>
              <button
                v-else
                @click="handleToggleFollow"
                @mouseenter="followHover = true"
                @mouseleave="followHover = false"
                :disabled="isFollowLoading"
                :class="[
                  'font-bold text-sm px-5 py-1.5 rounded-full transition-colors cursor-pointer min-w-[104px] disabled:opacity-50',
                  userInfo?.isFollowing
                    ? followHover
                      ? 'border border-red-800 bg-red-950/30 text-red-500'
                      : 'border border-neutral-600 bg-black text-white'
                    : 'bg-white hover:bg-neutral-200 text-black'
                ]"
              >
                {{ userInfo?.isFollowing ? (followHover ? 'Unfollow' : 'Following') : 'Follow' }}
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
              <span class="font-bold text-white">{{ userInfo?.followingCount ?? 0 }}</span>
              <span class="text-neutral-500 ml-1">Following</span>
            </div>
            <div class="hover:underline cursor-pointer">
              <span class="font-bold text-white">{{ userInfo?.followersCount ?? 0 }}</span>
              <span class="text-neutral-500 ml-1">{{ (userInfo?.followersCount ?? 0) === 1 ? 'Follower' : 'Followers' }}</span>
            </div>
          </div>
        </div>

        <!-- Profile Tabs -->
        <div class="flex border-b border-neutral-800 text-sm font-bold select-none overflow-x-auto">
          <button
            @click="switchTab('posts')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
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
            @click="switchTab('replies')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
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
            @click="switchTab('reposts')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
              activeTab === 'reposts' ? 'text-white font-bold' : 'text-neutral-500'
            ]"
          >
            Reposts
            <div
              v-if="activeTab === 'reposts'"
              class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
            ></div>
          </button>

          <button
            @click="switchTab('highlights')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
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
            @click="switchTab('media')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
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
            @click="switchTab('likes')"
            :class="[
              'flex-1 min-w-[80px] py-3.5 text-center hover:bg-neutral-900/60 transition-colors relative cursor-pointer',
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
          @click="() => fetchFeed(getActiveFeedTab())"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3.5 h-3.5" />
          <span>Retry</span>
        </button>
      </div>

      <!-- Non-Posts / Non-Replies / Non-Reposts Tab Empty State -->
      <div v-else-if="activeTab !== 'posts' && activeTab !== 'replies' && activeTab !== 'reposts'" class="flex flex-col items-center justify-center p-12 text-center">
        <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
          <MessageSquare class="w-8 h-8 text-neutral-600" />
        </div>
        <h3 class="text-xl font-bold mb-1">No {{ activeTab }} yet</h3>
        <p class="text-neutral-500 text-sm max-w-xs">
          When there are {{ activeTab }} to display, they will show up here.
        </p>
      </div>

      <!-- Posts Tab Empty State -->
      <div v-else-if="activeTab === 'posts' && posts.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
        <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
          <MessageSquare class="w-8 h-8 text-neutral-600" />
        </div>
        <h3 class="text-xl font-bold mb-1">No posts yet</h3>
        <p class="text-neutral-500 text-sm max-w-xs">
          {{ isMyProfile ? "When you post, your posts will show up here." : "When this user posts, their posts will show up here." }}
        </p>
      </div>

      <!-- Posts List -->
      <div v-else-if="activeTab === 'posts'" class="flex flex-col">
        <div
          v-for="post in posts"
          :key="post.id"
          class="flex flex-col border-b border-neutral-800"
        >
          <TweetCard
            :id="post.id"
            :user-id="profileAuthor?.id ?? targetUserId"
            :author="profileDisplayName"
            :handle="'@' + profileEmail"
            :avatar="profileAvatar"
            :content="post.text || ''"
            :image-url="post.imageUrl"
            :timestamp="formatDate(post.createdAt)"
            :updated-at="post.updatedAt"
            :retweets="post.repostCount"
            :replies="post.replyCount"
            :can-edit="isMyProfile"
            :is-reposted="Boolean(post.isReposted) || myRepostedPostIds.has(post.id)"
            :is-bookmarked="Boolean(post.isBookmarked) || myBookmarkedPostIds.has(post.id)"
            @updated="handlePostUpdated"
            @delete="handlePostDeleted"
            @reposted="handlePostReposted"
            @undo-repost="handlePostUndoReposted"
            @bookmarked="handlePostBookmarked"
            @undo-bookmark="handlePostUndoBookmarked"
            class="!border-b-0"
          />
        </div>
      </div>

      <!-- Replies Tab -->
      <div v-else-if="activeTab === 'replies'" class="flex flex-col">
        <!-- Replies Tab Empty State -->
        <div v-if="replies.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
          <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
            <MessageSquare class="w-8 h-8 text-neutral-600" />
          </div>
          <h3 class="text-xl font-bold mb-1">No replies yet</h3>
          <p class="text-neutral-500 text-sm max-w-xs">
            {{ isMyProfile ? "When you reply, your replies will show up here." : "When this user replies, their replies will show up here." }}
          </p>
        </div>

        <!-- Replies List with Parent Post thread context -->
        <div v-else class="flex flex-col">
          <div
            v-for="reply in replies"
            :key="reply.id"
            class="flex flex-col border-b border-neutral-800"
          >
            <!-- Parent Post Context -->
            <div
              v-if="reply.parentPost"
              @click="router.push(`/post/${reply.parentPost.id}`)"
              class="px-4 pt-3 pb-1 flex gap-3 relative hover:bg-neutral-950/50 transition-colors cursor-pointer"
            >
              <!-- Avatar & vertical thread line to reply -->
              <div class="flex flex-col items-center">
                <img
                  v-if="reply.parentPost.author"
                  :src="getAvatar(reply.parentPost.author.id)"
                  @click.stop="router.push(`/profile/${reply.parentPost.author.id}`)"
                  class="w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800 hover:opacity-90"
                  alt="Parent Author Avatar"
                />
                <div
                  v-else
                  class="w-10 h-10 rounded-full bg-neutral-800 border border-neutral-700 flex items-center justify-center shrink-0 text-neutral-400 text-xs font-bold"
                >
                  #{{ reply.parentPost.id }}
                </div>
                <div class="w-0.5 bg-neutral-700/80 grow mt-1.5 -mb-2"></div>
              </div>

              <!-- Parent Post Body -->
              <div class="flex flex-col gap-1 w-full pb-2">
                <div class="flex items-center gap-1.5 text-xs text-neutral-500 flex-wrap">
                  <template v-if="reply.parentPost.author">
                    <span
                      @click.stop="router.push(`/profile/${reply.parentPost.author.id}`)"
                      class="font-bold text-white hover:underline"
                    >
                      {{ reply.parentPost.author.email.split('@')[0] }}
                    </span>
                    <span class="text-neutral-500">
                      @{{ reply.parentPost.author.email }}
                    </span>
                  </template>
                  <span v-else class="font-bold text-neutral-300">
                    Post #{{ reply.parentPost.id }}
                  </span>
                  <span class="text-neutral-600">·</span>
                  <span class="text-neutral-500 hover:underline">{{ formatDate(reply.parentPost.createdAt) }}</span>
                </div>

                <div
                  v-if="reply.parentPost.replyId"
                  class="text-xs text-neutral-500 flex items-center gap-1 -mt-0.5"
                >
                  <span>Replying to</span>
                  <span
                    v-if="reply.parentPost.replyToAuthor"
                    @click.stop="router.push(`/profile/${reply.parentPost.replyToAuthor.id}`)"
                    class="text-sky-500 font-medium hover:underline cursor-pointer"
                  >
                    @{{ reply.parentPost.replyToAuthor.email.split('@')[0] }}
                  </span>
                  <span v-else class="text-neutral-500 italic">
                    a deleted post
                  </span>
                </div>

                <p v-if="reply.parentPost.text" class="text-sm leading-relaxed text-neutral-300 whitespace-pre-line">
                  {{ reply.parentPost.text }}
                </p>

                <div v-if="reply.parentPost.imageUrl" class="mt-2 rounded-2xl overflow-hidden border border-neutral-800 max-h-60 max-w-md">
                  <ImageLightbox
                    :src="reply.parentPost.imageUrl"
                    alt="Parent post image"
                    img-class="w-full h-full object-cover"
                  />
                </div>
              </div>
            </div>

            <!-- Soft-deleted Parent Post Indicator (Option B) -->
            <div v-else-if="reply.parentPost === null" class="px-4 pt-3 pb-1 flex gap-3 relative">
              <div class="flex flex-col items-center">
                <div class="w-10 h-10 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center shrink-0 text-neutral-500">
                  <AlertCircle class="w-4 h-4" />
                </div>
                <div class="w-0.5 bg-neutral-800 grow mt-1.5 -mb-2"></div>
              </div>

              <div class="flex flex-col gap-1 w-full pb-2 justify-center">
                <span class="text-xs text-neutral-500 italic bg-neutral-900/60 border border-neutral-800/80 rounded-xl px-3 py-2 w-fit">
                  This post was deleted.
                </span>
              </div>
            </div>

            <!-- Reply Card -->
            <div class="relative">
              <TweetCard
                :id="reply.id"
                :user-id="profileAuthor?.id ?? targetUserId"
                :author="profileDisplayName"
                :handle="'@' + profileEmail"
                :avatar="profileAvatar"
                :content="reply.text || ''"
                :image-url="reply.imageUrl"
                :timestamp="formatDate(reply.createdAt)"
                :updated-at="reply.updatedAt"
                :retweets="reply.repostCount"
                :replies="reply.replyCount"
                :reply-id="reply.parentPost?.id ?? -1"
                :reply-to-author="reply.parentPost?.author ?? null"
                :can-edit="isMyProfile"
                :is-reposted="Boolean(reply.isReposted) || myRepostedPostIds.has(reply.id)"
                :is-bookmarked="Boolean(reply.isBookmarked) || myBookmarkedPostIds.has(reply.id)"
                @updated="handleReplyUpdated"
                @delete="handleReplyDeleted"
                @reposted="handlePostReposted"
                @undo-repost="handlePostUndoReposted"
                @bookmarked="handlePostBookmarked"
                @undo-bookmark="handlePostUndoBookmarked"
                class="!border-b-0 !pt-2"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Reposts Tab -->
      <div v-else-if="activeTab === 'reposts'" class="flex flex-col">
        <!-- Reposts Tab Empty State -->
        <div v-if="reposts.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
          <div class="w-16 h-16 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
            <Repeat2 class="w-8 h-8 text-neutral-600" />
          </div>
          <h3 class="text-xl font-bold mb-1">No reposts yet</h3>
          <p class="text-neutral-500 text-sm max-w-xs">
            {{ isMyProfile ? "When you repost posts, they will show up here." : "When this user reposts posts, they will show up here." }}
          </p>
        </div>

        <!-- Reposts List -->
        <div v-else class="flex flex-col">
          <div
            v-for="repost in reposts"
            :key="repost.id"
            class="flex flex-col border-b border-neutral-800"
          >
            <div class="px-4 pt-2.5 -mb-2 flex items-center gap-2 text-xs font-semibold text-neutral-500">
              <Repeat2 class="w-3.5 h-3.5 text-neutral-500 ml-6" />
              <span>{{ isMyProfile ? 'You reposted' : `${profileDisplayName} reposted` }}</span>
            </div>

            <TweetCard
              :id="repost.id"
              :user-id="repost.author.id"
              :author="getAuthorDisplayName(repost.author)"
              :handle="'@' + repost.author.email"
              :avatar="getAvatar(repost.author.id)"
              :content="repost.text || ''"
              :image-url="repost.imageUrl"
              :timestamp="formatDate(repost.createdAt)"
              :updated-at="repost.updatedAt"
              :retweets="repost.repostCount"
              :replies="repost.replyCount"
              :reply-id="repost.replyId"
              :reply-to-author="repost.replyToAuthor"
              :can-edit="authStore.isAuthenticated && Number(authStore.currentUserId) === repost.author.id"
              :is-reposted="Boolean(repost.isReposted) || myRepostedPostIds.has(repost.id)"
              :is-bookmarked="Boolean(repost.isBookmarked) || myBookmarkedPostIds.has(repost.id)"
              @updated="handlePostUpdated"
              @delete="handlePostDeleted"
              @reposted="handlePostReposted"
              @undo-repost="handlePostUndoReposted"
              @bookmarked="handlePostBookmarked"
              @undo-bookmark="handlePostUndoBookmarked"
              class="!border-b-0"
            />
          </div>
        </div>
      </div>
    </div>

    <!-- Edit Profile & Account Settings Modal -->
    <EditProfileModal
      v-if="showEditModal"
      :user-id="targetUserId"
      :initial-username="userInfo?.username ?? authStore.currentUsername"
      :initial-image-url="userInfo?.imageUrl ?? authStore.currentUserImageUrl"
      :current-email="profileEmail"
      @close="showEditModal = false"
      @profile-updated="handleProfileUpdated"
    />
  </main>
</template>