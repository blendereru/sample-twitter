<script setup lang="ts">
import { ref, computed, onMounted, watch, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ArrowLeft, Loader2, AlertCircle, RefreshCw, MessageSquare, Image as ImageIcon, X } from 'lucide-vue-next';
import TweetCard from '@/components/tweet/TweetCard.vue';
import ImageLightbox from '@/components/common/ImageLightbox.vue';
import { getPostById, getPostReplies, createPost } from '@/api/posts';
import { uploadImage, validateImageFile, ALLOWED_IMAGE_ACCEPT } from '@/api/images';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';
import type { PostResponse, RepostResponse, BookmarkResponse } from '@/types/api';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const postId = computed<number>(() => Number(route.params.id));

const loading = ref(true);
const error = ref<string | null>(null);
const isNotFound = ref(false);

const post = ref<PostResponse | null>(null);
const parentPost = ref<PostResponse | null>(null);
const parentPostDeleted = ref(false);
const replies = ref<PostResponse[]>([]);
const myRepostedIds = ref<Set<number>>(new Set());
const myBookmarkedIds = ref<Set<number>>(new Set());

const replyTextareaRef = ref<HTMLTextAreaElement | null>(null);
const replyFileInputRef = ref<HTMLInputElement | null>(null);
const replyText = ref('');
const replyImageUrl = ref('');
const submittingReply = ref(false);
const uploadingReplyImage = ref(false);
const replySubmitError = ref<string | null>(null);

const remainingReplyChars = computed(() => 280 - replyText.value.length);
const isReplyValid = computed(() => {
  const hasText = replyText.value.trim().length > 0;
  const hasImage = replyImageUrl.value.trim().length > 0;
  return (hasText || hasImage) && remainingReplyChars.value >= 0 && !uploadingReplyImage.value;
});

function triggerReplyFileInput() {
  if (submittingReply.value || uploadingReplyImage.value) return;
  replyFileInputRef.value?.click();
}

async function handleReplyFileSelect(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;

  const validationError = validateImageFile(file);
  if (validationError) {
    replySubmitError.value = validationError;
    return;
  }

  uploadingReplyImage.value = true;
  replySubmitError.value = null;

  try {
    const res = await uploadImage(file);
    replyImageUrl.value = res.url;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    replySubmitError.value = parsed.detail || parsed.message || 'Failed to upload image.';
  } finally {
    uploadingReplyImage.value = false;
  }
}

function clearReplyImage() {
  replyImageUrl.value = '';
}

function getAvatar(userId: number): string {
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
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

function focusReplyInput() {
  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }
  replyTextareaRef.value?.focus();
}

async function fetchPostAndReplies(options: { silent?: boolean } = {}) {
  if (!postId.value || isNaN(postId.value) || postId.value <= 0) {
    isNotFound.value = true;
    loading.value = false;
    return;
  }

  if (!options.silent) {
    loading.value = true;
  }
  error.value = null;
  isNotFound.value = false;
  parentPost.value = null;
  parentPostDeleted.value = false;

  try {
    const [postData, repliesData] = await Promise.all([
      getPostById(postId.value),
      getPostReplies(postId.value),
    ]);
    post.value = postData;
    replies.value = repliesData || [];

    if (postData.replyId) {
      try {
        parentPost.value = await getPostById(postData.replyId);
      } catch {
        parentPost.value = null;
        parentPostDeleted.value = true;
      }
    }

    if (route.query.reply === '1') {
      await nextTick();
      replyTextareaRef.value?.focus();
    }
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 404) {
      isNotFound.value = true;
      error.value = 'This post does not exist or has been deleted.';
    } else {
      error.value = parsed.detail || parsed.message || 'Failed to load post and replies.';
    }
  } finally {
    if (!options.silent) {
      loading.value = false;
    }
  }
}

async function handleReplySubmit() {
  if (!isReplyValid.value || submittingReply.value || uploadingReplyImage.value || !post.value) return;

  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }

  submittingReply.value = true;
  replySubmitError.value = null;

  try {
    const text = replyText.value.trim() || undefined;
    const img = replyImageUrl.value.trim() || undefined;

    await createPost({
      text,
      imageUrl: img,
      replyId: post.value.id,
    });

    replyText.value = '';
    replyImageUrl.value = '';

    const updatedReplies = await getPostReplies(post.value.id);
    replies.value = updatedReplies || [];
    post.value.replyCount = replies.value.length;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.Text?.length) {
      replySubmitError.value = parsed.fieldErrors.Text[0];
    } else if (parsed.fieldErrors?.ImageUrl?.length) {
      replySubmitError.value = parsed.fieldErrors.ImageUrl[0];
    } else {
      replySubmitError.value = parsed.detail || parsed.message || 'Failed to post reply.';
    }
  } finally {
    submittingReply.value = false;
  }
}

function handleMainPostUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  if (post.value && post.value.id === updated.id) {
    post.value.text = updated.text;
    post.value.imageUrl = updated.imageUrl;
    post.value.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handleMainPostDeleted() {
  if (post.value?.replyId) {
    router.push(`/post/${post.value.replyId}`);
  } else {
    router.push('/');
  }
}

function handleReplyUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = replies.value.find(r => r.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handleReplyDeleted(id: number) {
  replies.value = replies.value.filter(r => r.id !== id);
  if (post.value) {
    post.value.replyCount = Math.max(0, (post.value.replyCount || 1) - 1);
  }
}

function handleReposted(response: RepostResponse) {
  myRepostedIds.value.add(response.postId);
  if (post.value && post.value.id === response.postId) {
    post.value.repostCount = (post.value.repostCount || 0) + 1;
  }
  const reply = replies.value.find(r => r.id === response.postId);
  if (reply) {
    reply.repostCount = (reply.repostCount || 0) + 1;
  }
}

function handleUndoRepost(id: number) {
  myRepostedIds.value.delete(id);
  if (post.value && post.value.id === id) {
    post.value.repostCount = Math.max(0, (post.value.repostCount || 0) - 1);
  }
  const reply = replies.value.find(r => r.id === id);
  if (reply) {
    reply.repostCount = Math.max(0, (reply.repostCount || 0) - 1);
  }
}

function handleBookmarked(response: BookmarkResponse) {
  myBookmarkedIds.value.add(response.postId);
  if (post.value && post.value.id === response.postId) {
    post.value.isBookmarked = true;
  }
  const reply = replies.value.find(r => r.id === response.postId);
  if (reply) {
    reply.isBookmarked = true;
  }
}

function handleUndoBookmark(id: number) {
  myBookmarkedIds.value.delete(id);
  if (post.value && post.value.id === id) {
    post.value.isBookmarked = false;
  }
  const reply = replies.value.find(r => r.id === id);
  if (reply) {
    reply.isBookmarked = false;
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
  fetchPostAndReplies();
});

watch(
  () => route.params.id,
  () => {
    fetchPostAndReplies();
  }
);
</script>

<template>
  <main class="flex flex-col min-h-screen border-r border-neutral-800 w-full max-w-2xl text-white">
    <!-- Sticky Header -->
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-20 flex items-center justify-between px-4 py-3">
      <div class="flex items-center gap-6">
        <button
          @click="goBack"
          class="p-2 rounded-full hover:bg-neutral-800 transition-colors cursor-pointer"
          title="Back"
        >
          <ArrowLeft class="w-5 h-5 text-white" />
        </button>
        <h2 class="text-lg font-bold leading-tight">Post</h2>
      </div>

      <button
        v-if="!loading && !isNotFound"
        @click="fetchPostAndReplies()"
        class="p-2 rounded-full hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer"
        title="Refresh thread"
      >
        <RefreshCw class="w-4 h-4" />
      </button>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="flex justify-center items-center py-20">
      <Loader2 class="w-8 h-8 text-sky-500 animate-spin" />
    </div>

    <!-- Not Found State -->
    <div v-else-if="isNotFound" class="flex flex-col items-center justify-center p-12 text-center my-8">
      <div class="w-20 h-20 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-4">
        <AlertCircle class="w-10 h-10 text-neutral-500" />
      </div>
      <h3 class="text-2xl font-bold mb-2">Post not found</h3>
      <p class="text-neutral-500 text-sm max-w-sm mb-6">
        {{ error || 'This post might have been deleted or does not exist.' }}
      </p>
      <button
        @click="router.push('/')"
        class="bg-sky-500 hover:bg-sky-400 text-white font-bold py-2.5 px-6 rounded-full text-sm transition-colors cursor-pointer"
      >
        Go to Home
      </button>
    </div>

    <!-- Error State -->
    <div v-else-if="error" class="p-8 text-center">
      <p class="text-sm text-red-400 mb-3">{{ error }}</p>
      <button
        @click="fetchPostAndReplies()"
        class="inline-flex items-center gap-2 px-4 py-2 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
      >
        <RefreshCw class="w-3.5 h-3.5" />
        <span>Retry</span>
      </button>
    </div>

    <!-- Post & Replies Thread -->
    <div v-else-if="post" class="flex flex-col">
      <!-- Parent Post Context (X-style thread line above focused reply) -->
      <div
        v-if="parentPost"
        @click="router.push(`/post/${parentPost.id}`)"
        class="px-4 pt-3 pb-1 flex gap-3 relative hover:bg-neutral-950/50 transition-colors cursor-pointer"
      >
        <div class="flex flex-col items-center">
          <img
            :src="getAvatar(parentPost.author.id)"
            @click.stop="router.push(`/profile/${parentPost.author.id}`)"
            class="w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800 hover:opacity-90"
            alt="Parent Author Avatar"
          />
          <div class="w-0.5 bg-neutral-700/80 grow mt-1.5 -mb-3"></div>
        </div>

        <div class="flex flex-col gap-1 w-full pb-2">
          <div class="flex items-center gap-1.5 text-sm flex-wrap">
            <span
              @click.stop="router.push(`/profile/${parentPost.author.id}`)"
              class="font-bold text-white hover:underline"
            >
              {{ parentPost.author.email.split('@')[0] }}
            </span>
            <span class="text-neutral-500">@{{ parentPost.author.email }}</span>
            <span class="text-neutral-600">·</span>
            <span class="text-neutral-500 hover:underline">{{ formatDate(parentPost.createdAt) }}</span>
          </div>

          <p v-if="parentPost.text" class="text-sm leading-relaxed text-neutral-300 whitespace-pre-line">
            {{ parentPost.text }}
          </p>

          <div v-if="parentPost.imageUrl" class="mt-2 rounded-2xl overflow-hidden border border-neutral-800 max-h-60 max-w-md">
            <ImageLightbox :src="parentPost.imageUrl" alt="Parent post image" img-class="w-full h-full object-cover" />
          </div>
        </div>
      </div>

      <!-- Deleted Parent Post Indicator -->
      <div v-else-if="parentPostDeleted" class="px-4 pt-3 pb-1 flex gap-3 relative">
        <div class="flex flex-col items-center">
          <div class="w-10 h-10 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center shrink-0 text-neutral-500">
            <AlertCircle class="w-4 h-4" />
          </div>
          <div class="w-0.5 bg-neutral-800 grow mt-1.5 -mb-3"></div>
        </div>
        <div class="flex flex-col gap-1 w-full pb-2 justify-center">
          <span class="text-xs text-neutral-500 italic bg-neutral-900/60 border border-neutral-800/80 rounded-xl px-3 py-2 w-fit">
            This post was deleted.
          </span>
        </div>
      </div>

      <!-- Focused Main Post Card -->
      <TweetCard
        :id="post.id"
        :user-id="post.author.id"
        :author="post.author.email.split('@')[0]"
        :handle="'@' + post.author.email"
        :avatar="getAvatar(post.author.id)"
        :content="post.text || ''"
        :image-url="post.imageUrl"
        :timestamp="formatDate(post.createdAt)"
        :created-at="post.createdAt"
        :updated-at="post.updatedAt"
        :retweets="post.repostCount"
        :replies="replies.length"
        :is-reposted="myRepostedIds.has(post.id)"
        :is-bookmarked="Boolean(post.isBookmarked) || myBookmarkedIds.has(post.id)"
        :is-focused="true"
        @updated="handleMainPostUpdated"
        @delete="handleMainPostDeleted"
        @reposted="handleReposted"
        @undo-repost="handleUndoRepost"
        @bookmarked="handleBookmarked"
        @undo-bookmark="handleUndoBookmark"
        @reply-click="focusReplyInput"
      />

      <!-- Reply Composer -->
      <div v-if="authStore.isAuthenticated" class="p-4 border-b border-neutral-800 flex flex-col gap-2">
        <div class="text-xs text-neutral-500 ml-13">
          Replying to <span class="text-sky-400">@{{ post.author.email }}</span>
        </div>

        <div v-if="replySubmitError" class="text-xs text-red-400 bg-red-950/40 border border-red-800/80 rounded-lg p-2.5">
          {{ replySubmitError }}
        </div>

        <div class="flex gap-3">
          <img
            :src="getAvatar(authStore.currentUserId || 1)"
            class="w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800"
            alt="Your Avatar"
          />

          <div class="flex flex-col w-full gap-2.5">
            <textarea
              ref="replyTextareaRef"
              v-model="replyText"
              rows="2"
              placeholder="Post your reply"
              :disabled="submittingReply"
              class="w-full bg-transparent text-white text-base placeholder-neutral-500 resize-none focus:outline-none disabled:opacity-50"
            ></textarea>

            <input
              ref="replyFileInputRef"
              type="file"
              :accept="ALLOWED_IMAGE_ACCEPT"
              class="hidden"
              @change="handleReplyFileSelect"
            />

            <div v-if="uploadingReplyImage" class="flex items-center gap-2 text-xs text-sky-400 bg-neutral-900/80 border border-neutral-800 rounded-xl p-2.5">
              <Loader2 class="w-4 h-4 animate-spin shrink-0" />
              <span>Uploading image...</span>
            </div>

            <div v-if="replyImageUrl.trim()" class="relative rounded-xl overflow-hidden border border-neutral-800 max-h-48">
              <ImageLightbox :src="replyImageUrl" alt="Preview" img-class="w-full h-full object-cover" />
              <button
                type="button"
                @click="clearReplyImage"
                title="Remove image"
                class="absolute top-2 right-2 bg-black/75 hover:bg-black text-white p-1.5 rounded-full transition-colors cursor-pointer"
              >
                <X class="w-4 h-4" />
              </button>
            </div>

            <div class="flex items-center justify-between pt-2 border-t border-neutral-800/80">
              <button
                type="button"
                @click="triggerReplyFileInput"
                :disabled="submittingReply || uploadingReplyImage"
                title="Upload Image"
                class="p-2 hover:bg-sky-500/10 rounded-full text-sky-400 transition-colors disabled:opacity-50 cursor-pointer"
              >
                <ImageIcon class="w-4 h-4" />
              </button>

              <div class="flex items-center gap-3">
                <span
                  v-if="replyText.length > 0"
                  :class="[
                    'text-xs',
                    remainingReplyChars < 0 ? 'text-red-500 font-bold' : remainingReplyChars <= 20 ? 'text-amber-400' : 'text-neutral-500'
                  ]"
                >
                  {{ remainingReplyChars }}
                </span>

                <button
                  type="button"
                  @click="handleReplySubmit"
                  :disabled="!isReplyValid || submittingReply || uploadingReplyImage"
                  class="bg-sky-500 hover:bg-sky-400 text-white font-bold px-4 py-1.5 rounded-full text-sm transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5 cursor-pointer"
                >
                  <Loader2 v-if="submittingReply" class="w-3.5 h-3.5 animate-spin" />
                  <span>Reply</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty Replies State -->
      <div v-if="replies.length === 0" class="flex flex-col items-center justify-center p-12 text-center">
        <div class="w-14 h-14 rounded-full bg-neutral-900 border border-neutral-800 flex items-center justify-center mb-3">
          <MessageSquare class="w-6 h-6 text-neutral-600" />
        </div>
        <h3 class="text-base font-bold mb-1">No replies yet</h3>
        <p class="text-neutral-500 text-xs max-w-xs">
          {{ authStore.isAuthenticated ? 'Start the conversation by replying above.' : 'Log in to reply to this post.' }}
        </p>
      </div>

      <!-- Replies Feed (Each reply is clickable to focus that reply & view its replies) -->
      <div v-else class="flex flex-col">
        <TweetCard
          v-for="reply in replies"
          :key="reply.id"
          :id="reply.id"
          :user-id="reply.author.id"
          :author="reply.author.email.split('@')[0]"
          :handle="'@' + reply.author.email"
          :avatar="getAvatar(reply.author.id)"
          :content="reply.text || ''"
          :image-url="reply.imageUrl"
          :timestamp="formatDate(reply.createdAt)"
          :updated-at="reply.updatedAt"
          :retweets="reply.repostCount"
          :replies="reply.replyCount"
          :is-reposted="myRepostedIds.has(reply.id)"
          :is-bookmarked="Boolean(reply.isBookmarked) || myBookmarkedIds.has(reply.id)"
          @updated="handleReplyUpdated"
          @delete="handleReplyDeleted"
          @reposted="handleReposted"
          @undo-repost="handleUndoRepost"
          @bookmarked="handleBookmarked"
          @undo-bookmark="handleUndoBookmark"
        />
      </div>
    </div>
  </main>
</template>
