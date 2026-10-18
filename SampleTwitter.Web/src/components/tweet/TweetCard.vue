<script setup lang="ts">
import { Heart, MessageCircle, Repeat2, Share, Bookmark, Pencil, X, Loader2, Image as ImageIcon, MoreHorizontal, Trash2, UserX, VolumeX, Flag, AlertCircle, RefreshCw, ExternalLink } from 'lucide-vue-next';
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';
import { createPost, editPost, deletePost, repostPost, undoRepost, getPostReplies } from '@/api/posts';
import { parseApiError } from '@/api/client';
import type { RepostResponse, PostResponse } from '@/types/api';

const props = defineProps<{
  id: number;
  userId?: number;
  canEdit?: boolean;
  author: string;
  handle: string;
  avatar: string;
  content: string;
  timestamp: string;
  updatedAt?: string;
  imageUrl?: string;
  likes?: number;
  retweets?: number;
  replies?: number;
  isReposted?: boolean;
  hideThreadLink?: boolean;
}>();

const emit = defineEmits<{
  (e: 'updated', post: { id: number; text?: string; imageUrl?: string; updatedAt?: string }): void;
  (e: 'delete', id: number): void;
  (e: 'reposted', response: RepostResponse): void;
  (e: 'undo-repost', id: number): void;
  (e: 'replied', payload: { parentPostId: number; replyId: number }): void;
}>();

const router = useRouter();
const authStore = useAuthStore();

const liked = ref(false);
const likeCount = ref(props.likes || 0);

const repostMenuOpen = ref(false);
const repostMenuRef = ref<HTMLElement | null>(null);

const isReposting = ref(false);
const reposted = ref(props.isReposted ?? false);
const repostCount = ref(props.retweets || 0);
const repostError = ref<string | null>(null);

const replyCount = ref(props.replies || 0);
const repliesOpen = ref(false);
const repliesList = ref<PostResponse[]>([]);
const loadingReplies = ref(false);
const repliesError = ref<string | null>(null);
const myRepostedReplyIds = ref<Set<number>>(new Set());

const replyText = ref('');
const replyImageUrl = ref('');
const showReplyImageInput = ref(false);
const submittingReply = ref(false);
const replySubmitError = ref<string | null>(null);

watch(
  () => props.retweets,
  (newVal) => {
    if (newVal !== undefined) repostCount.value = newVal;
  }
);

watch(
  () => props.replies,
  (newVal) => {
    if (newVal !== undefined) replyCount.value = newVal;
  }
);

watch(
  () => props.isReposted,
  (newVal) => {
    if (newVal !== undefined) reposted.value = newVal;
  }
);

function toggleRepostMenu() {
  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }
  repostMenuOpen.value = !repostMenuOpen.value;
  if (repostMenuOpen.value) {
    menuOpen.value = false;
  }
}

async function executeRepost() {
  repostMenuOpen.value = false;
  if (isReposting.value) return;

  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }

  if (props.id <= 0) {
    repostError.value = 'Cannot repost demo tweets.';
    setTimeout(() => {
      repostError.value = null;
    }, 3000);
    return;
  }

  isReposting.value = true;
  repostError.value = null;

  try {
    const result = await repostPost(props.id);
    reposted.value = true;
    repostCount.value += 1;
    emit('reposted', result);
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 409) {
      reposted.value = true;
      repostError.value = 'You already reposted this post.';
    } else {
      repostError.value = parsed.detail || parsed.message || 'Failed to repost.';
    }
    setTimeout(() => {
      repostError.value = null;
    }, 4000);
  } finally {
    isReposting.value = false;
  }
}

async function executeUndoRepost() {
  repostMenuOpen.value = false;
  if (isReposting.value) return;

  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }

  if (props.id <= 0) {
    repostError.value = 'Cannot undo repost on demo tweets.';
    setTimeout(() => {
      repostError.value = null;
    }, 3000);
    return;
  }

  isReposting.value = true;
  repostError.value = null;

  try {
    await undoRepost(props.id);
    reposted.value = false;
    repostCount.value = Math.max(0, repostCount.value - 1);
    emit('undo-repost', props.id);
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.status === 404) {
      reposted.value = false;
      emit('undo-repost', props.id);
    } else {
      repostError.value = parsed.detail || parsed.message || 'Failed to undo repost.';
    }
    setTimeout(() => {
      repostError.value = null;
    }, 4000);
  } finally {
    isReposting.value = false;
  }
}

function handleQuote() {
  repostMenuOpen.value = false;
  repostError.value = 'Quote tweets are coming soon!';
  setTimeout(() => {
    repostError.value = null;
  }, 3000);
}

const menuOpen = ref(false);
const menuRef = ref<HTMLElement | null>(null);

const isEditing = ref(false);
const editText = ref('');
const editImageUrl = ref('');
const showImageInput = ref(false);
const loading = ref(false);
const error = ref<string | null>(null);

const showDeleteModal = ref(false);
const isDeleting = ref(false);
const deleteError = ref<string | null>(null);

const isAuthor = computed(() => {
  if (props.canEdit !== undefined) return props.canEdit;
  if (!authStore.isAuthenticated) return false;
  const currentId = authStore.currentUserId ?? (localStorage.getItem('sampletwitter_user_id') ? Number(localStorage.getItem('sampletwitter_user_id')) : null);
  const currentEmail = authStore.currentUserEmail || localStorage.getItem('sampletwitter_user_email');

  if (props.userId !== undefined && props.userId !== null && currentId !== null) {
    if (Number(props.userId) === Number(currentId)) return true;
  }
  if (currentEmail && (props.handle === currentEmail || props.handle === `@${currentEmail}`)) {
    return true;
  }
  return false;
});


const remainingChars = computed(() => 280 - editText.value.length);
const isValid = computed(() => {
  const hasText = editText.value.trim().length > 0;
  const hasImage = editImageUrl.value.trim().length > 0;
  return (hasText || hasImage) && remainingChars.value >= 0;
});

const remainingReplyChars = computed(() => 280 - replyText.value.length);
const isReplyValid = computed(() => {
  const hasText = replyText.value.trim().length > 0;
  const hasImage = replyImageUrl.value.trim().length > 0;
  return (hasText || hasImage) && remainingReplyChars.value >= 0;
});

function getAvatar(userId: number): string {
  return userId % 2 === 0
    ? 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=120&auto=format&fit=crop&q=80'
    : 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=120&auto=format&fit=crop&q=80';
}

function goToProfile(userId?: number) {
  if (userId && userId > 0) {
    router.push(`/profile/${userId}`);
  }
}

function goToPostThread() {
  if (props.id > 0) {
    router.push(`/post/${props.id}`);
  }
}

async function loadReplies(options: { silent?: boolean } = {}) {
  if (props.id <= 0) return;

  if (!options.silent) {
    loadingReplies.value = true;
  }
  repliesError.value = null;

  try {
    const data = await getPostReplies(props.id);
    repliesList.value = data || [];
    replyCount.value = repliesList.value.length;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    repliesError.value = parsed.detail || parsed.message || 'Failed to load replies.';
  } finally {
    if (!options.silent) {
      loadingReplies.value = false;
    }
  }
}

function toggleReplies() {
  repliesOpen.value = !repliesOpen.value;
  if (repliesOpen.value && props.id > 0) {
    loadReplies();
  }
}

async function submitReply() {
  if (!isReplyValid.value || submittingReply.value) return;

  if (!authStore.isAuthenticated) {
    router.push('/login');
    return;
  }

  if (props.id <= 0) {
    replySubmitError.value = 'Cannot reply to demo tweets.';
    return;
  }

  submittingReply.value = true;
  replySubmitError.value = null;

  try {
    const text = replyText.value.trim() || undefined;
    const img = replyImageUrl.value.trim() || undefined;

    const response = await createPost({
      text,
      imageUrl: img,
      replyId: props.id,
    });

    replyText.value = '';
    replyImageUrl.value = '';
    showReplyImageInput.value = false;
    replyCount.value += 1;

    emit('replied', { parentPostId: props.id, replyId: response.postId });
    await loadReplies({ silent: true });
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

function handleChildReplyUpdated(updated: { id: number; text?: string; imageUrl?: string; updatedAt?: string }) {
  const item = repliesList.value.find(r => r.id === updated.id);
  if (item) {
    item.text = updated.text;
    item.imageUrl = updated.imageUrl;
    item.updatedAt = updated.updatedAt || new Date().toISOString();
  }
}

function handleChildReplyDeleted(replyId: number) {
  repliesList.value = repliesList.value.filter(r => r.id !== replyId);
  replyCount.value = Math.max(0, replyCount.value - 1);
}

function handleChildReplyReposted(response: RepostResponse) {
  myRepostedReplyIds.value.add(response.postId);
  const item = repliesList.value.find(r => r.id === response.postId);
  if (item) {
    item.repostCount = (item.repostCount || 0) + 1;
  }
}

function handleChildReplyUndoReposted(postId: number) {
  myRepostedReplyIds.value.delete(postId);
  const item = repliesList.value.find(r => r.id === postId);
  if (item) {
    item.repostCount = Math.max(0, (item.repostCount || 0) - 1);
  }
}

function handleChildReplyReplied(payload: { parentPostId: number; replyId: number }) {
  const item = repliesList.value.find(r => r.id === payload.parentPostId);
  if (item) {
    item.replyCount = (item.replyCount || 0) + 1;
  }
}

function toggleLike() {
  liked.value = !liked.value;
  likeCount.value += liked.value ? 1 : -1;
}

function toggleMenu() {
  menuOpen.value = !menuOpen.value;
  if (menuOpen.value) {
    repostMenuOpen.value = false;
  }
}

function handleEditClick() {
  menuOpen.value = false;
  startEditing();
}

function handleDeleteClick() {
  menuOpen.value = false;
  deleteError.value = null;
  showDeleteModal.value = true;
}

async function confirmDelete() {
  if (isDeleting.value) return;

  isDeleting.value = true;
  deleteError.value = null;

  try {
    if (props.id > 0) {
      await deletePost(props.id);
    }
    showDeleteModal.value = false;
    emit('delete', props.id);
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    deleteError.value = parsed.detail || parsed.message || 'Failed to delete post.';
  } finally {
    isDeleting.value = false;
  }
}

function cancelDelete() {
  if (isDeleting.value) return;
  showDeleteModal.value = false;
  deleteError.value = null;
}

function handleClickOutside(event: MouseEvent) {
  if (menuOpen.value && menuRef.value && !menuRef.value.contains(event.target as Node)) {
    menuOpen.value = false;
  }
  if (repostMenuOpen.value && repostMenuRef.value && !repostMenuRef.value.contains(event.target as Node)) {
    repostMenuOpen.value = false;
  }
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    if (repostMenuOpen.value) {
      repostMenuOpen.value = false;
    }
    if (menuOpen.value) {
      menuOpen.value = false;
    }
    if (showDeleteModal.value) {
      cancelDelete();
    }
  }
}

onMounted(() => {
  window.addEventListener('click', handleClickOutside);
  window.addEventListener('keydown', handleKeydown);
});

onUnmounted(() => {
  window.removeEventListener('click', handleClickOutside);
  window.removeEventListener('keydown', handleKeydown);
});

function startEditing() {
  editText.value = props.content || '';
  editImageUrl.value = props.imageUrl || '';
  showImageInput.value = !!props.imageUrl;
  error.value = null;
  isEditing.value = true;
}

function cancelEditing() {
  isEditing.value = false;
  error.value = null;
}

const currentUpdatedAt = ref<string | undefined>(props.updatedAt);
watch(
  () => props.updatedAt,
  (newVal) => {
    currentUpdatedAt.value = newVal;
  }
);

function formatTime(dateStr?: string): string {
  if (!dateStr) return '';
  try {
    const date = new Date(dateStr);
    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);
    if (diffSec < 60) return 'Just now';
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  } catch {
    return dateStr;
  }
}

function formatFullDateTime(dateStr?: string): string {
  if (!dateStr) return '';
  try {
    const date = new Date(dateStr);
    return date.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
  } catch {
    return dateStr;
  }
}

const formattedUpdatedAt = computed(() => {
  if (!currentUpdatedAt.value) return null;
  return formatTime(currentUpdatedAt.value);
});

const fullUpdatedAtTooltip = computed(() => {
  if (!currentUpdatedAt.value) return '';
  return `Last edited: ${formatFullDateTime(currentUpdatedAt.value)}`;
});

async function handleSave() {
  if (!isValid.value || loading.value) return;

  loading.value = true;
  error.value = null;

  try {
    const text = editText.value.trim() || undefined;
    const img = editImageUrl.value.trim() || undefined;

    const result = await editPost(props.id, {
      text,
      imageUrl: img,
    });

    currentUpdatedAt.value = result.updatedAt;

    emit('updated', {
      id: props.id,
      text,
      imageUrl: img,
      updatedAt: result.updatedAt,
    });

    isEditing.value = false;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.Text?.length) {
      error.value = parsed.fieldErrors.Text[0];
    } else if (parsed.fieldErrors?.ImageUrl?.length) {
      error.value = parsed.fieldErrors.ImageUrl[0];
    } else {
      error.value = parsed.detail || parsed.message || 'Failed to update post.';
    }
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="border-b border-neutral-800 flex flex-col">
    <article class="p-4 hover:bg-neutral-950/40 transition-colors flex gap-3">
      <img
        :src="avatar"
        @click.stop="goToProfile(userId)"
        :class="['w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800', userId && userId > 0 ? 'cursor-pointer hover:opacity-90' : '']"
        alt="Avatar"
      />

      <div class="flex flex-col gap-1.5 w-full">
        <div class="flex items-center justify-between text-sm">
          <div class="flex items-center gap-1.5 flex-wrap">
            <span
              @click.stop="goToProfile(userId)"
              :class="['font-bold text-white hover:underline', userId && userId > 0 ? 'cursor-pointer' : '']"
            >{{ author }}</span>
            <span class="text-neutral-500">{{ handle }}</span>
            <span class="text-neutral-600">·</span>
            <span
              @click.stop="goToPostThread"
              :class="['text-neutral-500 hover:underline', id > 0 ? 'cursor-pointer' : '']"
            >{{ timestamp }}</span>
          <span
            v-if="currentUpdatedAt && formattedUpdatedAt"
            class="text-xs text-neutral-400 hover:text-neutral-300 flex items-center gap-1 cursor-default transition-colors"
            :title="fullUpdatedAtTooltip"
          >
            <span class="text-neutral-600">·</span>
            <Pencil class="w-3 h-3 text-neutral-400 inline shrink-0" />
            <span>Edited {{ formattedUpdatedAt }}</span>
          </span>
        </div>

        <div v-if="!isEditing" class="relative" ref="menuRef">
          <button
            type="button"
            @click.stop="toggleMenu"
            class="p-1.5 rounded-full text-neutral-500 hover:text-sky-400 hover:bg-sky-500/10 transition-colors"
            title="More"
          >
            <MoreHorizontal class="w-4 h-4" />
          </button>

          <div
            v-if="menuOpen"
            class="absolute right-0 top-8 w-48 bg-black border border-neutral-800 rounded-2xl shadow-2xl py-1.5 z-30 overflow-hidden"
            @click.stop
          >
            <template v-if="isAuthor">
              <button
                type="button"
                @click="handleEditClick"
                class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 flex items-center gap-3 transition-colors font-medium"
              >
                <Pencil class="w-4 h-4 text-neutral-400" />
                <span>Edit</span>
              </button>
              <button
                type="button"
                @click="handleDeleteClick"
                class="w-full px-4 py-2.5 text-left text-sm text-red-500 hover:bg-neutral-900 flex items-center gap-3 transition-colors font-medium"
              >
                <Trash2 class="w-4 h-4 text-red-500" />
                <span>Delete</span>
              </button>
            </template>

            <template v-else>
              <button
                type="button"
                @click="menuOpen = false"
                class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 flex items-center gap-3 transition-colors font-medium"
              >
                <UserX class="w-4 h-4 text-neutral-400" />
                <span>Unfollow {{ handle }}</span>
              </button>
              <button
                type="button"
                @click="menuOpen = false"
                class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 flex items-center gap-3 transition-colors font-medium"
              >
                <VolumeX class="w-4 h-4 text-neutral-400" />
                <span>Mute {{ handle }}</span>
              </button>
              <button
                type="button"
                @click="menuOpen = false"
                class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 flex items-center gap-3 transition-colors font-medium"
              >
                <Flag class="w-4 h-4 text-neutral-400" />
                <span>Report post</span>
              </button>
            </template>
          </div>
        </div>
      </div>



      <div v-if="isEditing" class="flex flex-col gap-2.5 mt-1" @click.stop>
        <div v-if="error" class="text-xs text-red-400 bg-red-950/40 border border-red-800/80 rounded-lg p-2.5">
          {{ error }}
        </div>

        <textarea
          v-model="editText"
          rows="3"
          placeholder="Edit your post..."
          :disabled="loading"
          class="w-full bg-neutral-900 border border-neutral-700 rounded-xl p-3 text-white text-sm placeholder-neutral-500 resize-none focus:outline-none focus:border-sky-500 disabled:opacity-50"
        ></textarea>

        <div v-if="showImageInput" class="flex items-center gap-2 bg-neutral-900 border border-neutral-800 rounded-xl p-2">
          <input
            v-model="editImageUrl"
            type="url"
            placeholder="Enter image URL"
            :disabled="loading"
            class="bg-transparent text-sm text-white placeholder-neutral-500 focus:outline-none w-full"
          />
          <button
            type="button"
            @click="showImageInput = false; editImageUrl = ''"
            class="text-neutral-400 hover:text-white p-1 rounded-full hover:bg-neutral-800"
          >
            <X class="w-4 h-4" />
          </button>
        </div>

        <div v-if="editImageUrl.trim()" class="relative rounded-xl overflow-hidden border border-neutral-800 max-h-48">
          <img :src="editImageUrl" alt="Preview" class="w-full h-full object-cover" />
        </div>

        <div class="flex items-center justify-between pt-1">
          <div class="flex items-center gap-2">
            <button
              type="button"
              @click="showImageInput = !showImageInput"
              title="Add Image URL"
              :class="['p-1.5 hover:bg-sky-500/10 rounded-full text-sky-400 transition-colors', showImageInput ? 'bg-sky-500/20 text-white' : '']"
            >
              <ImageIcon class="w-4 h-4" />
            </button>
            <span
              v-if="editText.length > 0"
              :class="[
                'text-xs',
                remainingChars < 0 ? 'text-red-500 font-bold' : remainingChars <= 20 ? 'text-amber-400' : 'text-neutral-500'
              ]"
            >
              {{ remainingChars }}
            </span>
          </div>

          <div class="flex items-center gap-2">
            <button
              type="button"
              @click="cancelEditing"
              :disabled="loading"
              class="px-3 py-1.5 rounded-full text-xs font-semibold text-neutral-300 hover:bg-neutral-800 transition-colors"
            >
              Cancel
            </button>
            <button
              type="button"
              @click="handleSave"
              :disabled="!isValid || loading"
              class="px-4 py-1.5 rounded-full text-xs font-bold bg-sky-500 hover:bg-sky-400 text-white disabled:opacity-50 transition-colors flex items-center gap-1.5"
            >
              <Loader2 v-if="loading" class="w-3 h-3 animate-spin" />
              <span>Save</span>
            </button>
          </div>
        </div>
      </div>

      <template v-else>
        <p v-if="content" class="text-sm leading-relaxed text-neutral-200 whitespace-pre-line">{{ content }}</p>

        <div v-if="imageUrl" class="mt-2 rounded-2xl overflow-hidden border border-neutral-800 max-h-96">
          <img :src="imageUrl" alt="Attachment" class="w-full h-full object-cover" />
        </div>

        <div class="flex items-center justify-between text-neutral-500 text-xs mt-2 max-w-md">
          <button
            type="button"
            @click.stop="toggleReplies"
            :class="[
              'flex items-center gap-1.5 group transition-colors cursor-pointer select-none',
              repliesOpen ? 'text-sky-400' : 'hover:text-sky-400 text-neutral-500'
            ]"
            title="Replies"
          >
            <div
              :class="[
                'p-2 rounded-full transition-colors',
                repliesOpen ? 'bg-sky-500/10 text-sky-400' : 'group-hover:bg-sky-500/10'
              ]"
            >
              <MessageCircle class="w-4 h-4" />
            </div>
            <span :class="repliesOpen ? 'text-sky-400 font-semibold' : ''">{{ replyCount }}</span>
          </button>

          <div class="relative" ref="repostMenuRef">
            <button
              type="button"
              @click.stop="toggleRepostMenu"
              :disabled="isReposting"
              :class="[
                'flex items-center gap-1.5 group transition-colors select-none',
                reposted ? 'text-emerald-500' : 'hover:text-emerald-400 text-neutral-500'
              ]"
              :title="reposted ? 'Undo repost' : 'Repost'"
            >
              <div
                :class="[
                  'p-2 rounded-full transition-all group-active:scale-90',
                  reposted ? 'bg-emerald-500/10 text-emerald-500' : 'group-hover:bg-emerald-500/10'
                ]"
              >
                <Loader2 v-if="isReposting" class="w-4 h-4 animate-spin text-emerald-500" />
                <Repeat2 v-else class="w-4 h-4" />
              </div>
              <span :class="reposted ? 'text-emerald-500 font-semibold' : ''">{{ repostCount }}</span>
            </button>

            <!-- Repost / Undo Repost Popover Menu -->
            <div
              v-if="repostMenuOpen"
              class="absolute left-0 bottom-full mb-1.5 z-30 w-44 bg-black border border-neutral-800 rounded-2xl shadow-2xl py-1.5 overflow-hidden select-none"
              @click.stop
            >
              <template v-if="!reposted">
                <button
                  type="button"
                  @click="executeRepost"
                  class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 hover:text-white flex items-center gap-3 transition-colors font-bold group/item cursor-pointer"
                >
                  <Repeat2 class="w-4.5 h-4.5 text-neutral-400 group-hover/item:text-emerald-400 transition-colors" />
                  <span>Repost</span>
                </button>
                <button
                  type="button"
                  @click="handleQuote"
                  class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 hover:text-white flex items-center gap-3 transition-colors font-bold group/item cursor-pointer"
                >
                  <Pencil class="w-4.5 h-4.5 text-neutral-400 group-hover/item:text-sky-400 transition-colors" />
                  <span>Quote</span>
                </button>
              </template>

              <template v-else>
                <button
                  type="button"
                  @click="executeUndoRepost"
                  class="w-full px-4 py-2.5 text-left text-sm text-red-500 hover:bg-red-500/10 flex items-center gap-3 transition-colors font-bold group/item cursor-pointer"
                >
                  <Repeat2 class="w-4.5 h-4.5 text-red-500 group-hover/item:text-red-400 transition-colors" />
                  <span class="group-hover/item:text-red-400">Undo repost</span>
                </button>
                <button
                  type="button"
                  @click="handleQuote"
                  class="w-full px-4 py-2.5 text-left text-sm text-neutral-200 hover:bg-neutral-900 hover:text-white flex items-center gap-3 transition-colors font-bold group/item cursor-pointer"
                >
                  <Pencil class="w-4.5 h-4.5 text-neutral-400 group-hover/item:text-sky-400 transition-colors" />
                  <span>Quote</span>
                </button>
              </template>
            </div>
          </div>

          <button
            @click.stop="toggleLike"
            :class="['flex items-center gap-1.5 group transition-colors', liked ? 'text-pink-500' : 'hover:text-pink-500']"
          >
            <div class="p-2 rounded-full group-hover:bg-pink-500/10">
              <Heart :class="['w-4 h-4', liked ? 'fill-current text-pink-500' : '']" />
            </div>
            <span>{{ likeCount }}</span>
          </button>

          <button class="flex items-center gap-1.5 hover:text-sky-400 group transition-colors">
            <div class="p-2 rounded-full group-hover:bg-sky-500/10">
              <Bookmark class="w-4 h-4" />
            </div>
          </button>

          <button class="flex items-center gap-1.5 hover:text-sky-400 group transition-colors">
            <div class="p-2 rounded-full group-hover:bg-sky-500/10">
              <Share class="w-4 h-4" />
            </div>
          </button>
        </div>

        <div
          v-if="repostError"
          class="mt-2 text-xs text-amber-400 bg-amber-950/40 border border-amber-900/50 rounded-lg p-2 flex items-center gap-1.5"
        >
          <AlertCircle class="w-3.5 h-3.5 shrink-0" />
          <span>{{ repostError }}</span>
        </div>
      </template>
      </div>
    </article>

    <!-- Expandable Replies Section -->
    <div
      v-if="repliesOpen"
      class="border-t border-neutral-800/80 bg-neutral-950/30 flex flex-col"
      @click.stop
    >
      <!-- Replies Header Bar -->
      <div class="px-4 py-2 border-b border-neutral-800/60 flex items-center justify-between text-xs text-neutral-400">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-neutral-300">
            Replies ({{ replyCount }})
          </span>
          <span class="text-neutral-600">·</span>
          <span>Replying to <span class="text-sky-400">{{ handle }}</span></span>
        </div>

        <div class="flex items-center gap-2">
          <button
            v-if="id > 0"
            type="button"
            @click="loadReplies()"
            :disabled="loadingReplies"
            class="p-1.5 rounded-full hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer disabled:opacity-50"
            title="Refresh replies"
          >
            <RefreshCw :class="['w-3.5 h-3.5', loadingReplies ? 'animate-spin text-sky-400' : '']" />
          </button>
          <button
            v-if="id > 0 && !hideThreadLink"
            type="button"
            @click="goToPostThread"
            class="inline-flex items-center gap-1 px-2.5 py-1 rounded-full hover:bg-neutral-800 text-sky-400 hover:text-sky-300 transition-colors cursor-pointer font-medium"
            title="Open post thread"
          >
            <span>Thread</span>
            <ExternalLink class="w-3 h-3" />
          </button>
        </div>
      </div>

      <!-- Inline Reply Composer -->
      <div v-if="id > 0 && authStore.isAuthenticated" class="px-4 py-3 border-b border-neutral-800/60 flex flex-col gap-2">
        <div v-if="replySubmitError" class="text-xs text-red-400 bg-red-950/40 border border-red-800/80 rounded-lg p-2">
          {{ replySubmitError }}
        </div>

        <div class="flex gap-3">
          <img
            :src="getAvatar(authStore.currentUserId || 1)"
            class="w-8 h-8 rounded-full object-cover shrink-0 bg-neutral-800 mt-0.5"
            alt="Your avatar"
          />

          <div class="flex flex-col w-full gap-2">
            <textarea
              v-model="replyText"
              rows="2"
              placeholder="Post your reply"
              :disabled="submittingReply"
              class="w-full bg-neutral-900/80 border border-neutral-800 rounded-xl p-2.5 text-white text-sm placeholder-neutral-500 resize-none focus:outline-none focus:border-sky-500 disabled:opacity-50"
            ></textarea>

            <div v-if="showReplyImageInput" class="flex items-center gap-2 bg-neutral-900 border border-neutral-800 rounded-xl p-2">
              <input
                v-model="replyImageUrl"
                type="url"
                placeholder="Enter image URL (e.g. https://...)"
                :disabled="submittingReply"
                class="bg-transparent text-xs text-white placeholder-neutral-500 focus:outline-none w-full"
              />
              <button
                type="button"
                @click="showReplyImageInput = false; replyImageUrl = ''"
                class="text-neutral-400 hover:text-white p-1 rounded-full hover:bg-neutral-800"
              >
                <X class="w-3.5 h-3.5" />
              </button>
            </div>

            <div v-if="replyImageUrl.trim()" class="relative rounded-xl overflow-hidden border border-neutral-800 max-h-40">
              <img :src="replyImageUrl" alt="Reply preview" class="w-full h-full object-cover" />
            </div>

            <div class="flex items-center justify-between">
              <button
                type="button"
                @click="showReplyImageInput = !showReplyImageInput"
                title="Add Image URL"
                :class="['p-1.5 hover:bg-sky-500/10 rounded-full text-sky-400 transition-colors cursor-pointer', showReplyImageInput ? 'bg-sky-500/20 text-white' : '']"
              >
                <ImageIcon class="w-4 h-4" />
              </button>

              <div class="flex items-center gap-2.5">
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
                  @click="submitReply"
                  :disabled="!isReplyValid || submittingReply"
                  class="bg-sky-500 hover:bg-sky-400 text-white font-bold px-3.5 py-1.5 rounded-full text-xs transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5 cursor-pointer"
                >
                  <Loader2 v-if="submittingReply" class="w-3 h-3 animate-spin" />
                  <span>Reply</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Demo Post Notice -->
      <div v-if="id <= 0" class="p-6 text-center text-xs text-neutral-500">
        Replies are disabled for sample demo posts. Create a post above to test replies!
      </div>

      <!-- Loading Replies State -->
      <div v-else-if="loadingReplies" class="flex items-center justify-center gap-2 py-8 text-xs text-neutral-400">
        <Loader2 class="w-5 h-5 text-sky-500 animate-spin" />
        <span>Loading replies...</span>
      </div>

      <!-- Error Loading Replies State -->
      <div v-else-if="repliesError" class="p-5 text-center flex flex-col items-center gap-2">
        <p class="text-xs text-red-400">{{ repliesError }}</p>
        <button
          type="button"
          @click="loadReplies()"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full border border-neutral-700 hover:bg-neutral-800 text-xs font-semibold text-neutral-300 transition-colors cursor-pointer"
        >
          <RefreshCw class="w-3 h-3" />
          <span>Retry</span>
        </button>
      </div>

      <!-- Empty Replies State -->
      <div v-else-if="repliesList.length === 0" class="py-6 px-4 text-center text-xs text-neutral-500">
        No replies yet. {{ authStore.isAuthenticated ? 'Be the first to reply!' : 'Log in to reply to this post.' }}
      </div>

      <!-- Replies List -->
      <div v-else class="flex flex-col divide-y divide-neutral-800/60 pl-4 sm:pl-6 border-l-2 border-neutral-800 ml-4 sm:ml-6 my-1">
        <TweetCard
          v-for="reply in repliesList"
          :key="'reply-' + reply.id"
          :id="reply.id"
          :user-id="reply.author.id"
          :author="reply.author.email.split('@')[0]"
          :handle="'@' + reply.author.email"
          :avatar="getAvatar(reply.author.id)"
          :content="reply.text || ''"
          :image-url="reply.imageUrl"
          :timestamp="formatTime(reply.createdAt)"
          :updated-at="reply.updatedAt"
          :retweets="reply.repostCount"
          :replies="reply.replyCount"
          :is-reposted="myRepostedReplyIds.has(reply.id)"
          @updated="handleChildReplyUpdated"
          @delete="handleChildReplyDeleted"
          @reposted="handleChildReplyReposted"
          @undo-repost="handleChildReplyUndoReposted"
          @replied="handleChildReplyReplied"
          class="!border-b-0"
        />
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <Teleport to="body">
      <div
        v-if="showDeleteModal"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-xs transition-opacity"
        @click="cancelDelete"
      >
        <div
          class="bg-black border border-neutral-800 rounded-2xl max-w-xs w-full p-6 flex flex-col gap-3 shadow-2xl"
          @click.stop
        >
          <h3 class="text-xl font-extrabold text-white leading-tight">Delete post?</h3>
          <p class="text-sm text-neutral-500 leading-relaxed">
            This can’t be undone and it will be removed from your profile, the timeline of any accounts that follow you, and from search results.
          </p>

          <div v-if="deleteError" class="text-xs text-red-400 bg-red-950/40 border border-red-800/80 rounded-lg p-2.5 mt-1">
            {{ deleteError }}
          </div>

          <div class="flex flex-col gap-2.5 mt-2">
            <button
              type="button"
              @click="confirmDelete"
              :disabled="isDeleting"
              class="w-full py-2.5 rounded-full font-bold text-sm bg-red-600 hover:bg-red-500 text-white transition-colors disabled:opacity-50 flex items-center justify-center gap-2 shadow-sm"
            >
              <Loader2 v-if="isDeleting" class="w-4 h-4 animate-spin" />
              <span>{{ isDeleting ? 'Deleting...' : 'Delete' }}</span>
            </button>

            <button
              type="button"
              @click="cancelDelete"
              :disabled="isDeleting"
              class="w-full py-2.5 rounded-full font-bold text-sm border border-neutral-700 hover:bg-neutral-900 text-white transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>