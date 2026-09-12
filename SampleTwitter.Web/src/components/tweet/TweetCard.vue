<script setup lang="ts">
import { Heart, MessageCircle, Repeat2, Share, Bookmark, Pencil, X, Loader2, Image as ImageIcon, MoreHorizontal, Trash2, UserX, VolumeX, Flag } from 'lucide-vue-next';
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useAuthStore } from '@/stores/auth';
import { editPost, deletePost } from '@/api/posts';
import { parseApiError } from '@/api/client';

const props = defineProps<{
  id: number;
  userId?: number;
  canEdit?: boolean;
  author: string;
  handle: string;
  avatar: string;
  content: string;
  timestamp: string;
  imageUrl?: string;
  likes?: number;
  retweets?: number;
  replies?: number;
}>();

const emit = defineEmits<{
  (e: 'updated', post: { id: number; text?: string; imageUrl?: string }): void;
  (e: 'delete', id: number): void;
}>();

const authStore = useAuthStore();

const liked = ref(false);
const likeCount = ref(props.likes || 0);

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
  if (currentEmail && props.handle === currentEmail) {
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

function toggleLike() {
  liked.value = !liked.value;
  likeCount.value += liked.value ? 1 : -1;
}

function toggleMenu() {
  menuOpen.value = !menuOpen.value;
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
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && showDeleteModal.value) {
    cancelDelete();
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

async function handleSave() {
  if (!isValid.value || loading.value) return;

  loading.value = true;
  error.value = null;

  try {
    const text = editText.value.trim() || undefined;
    const img = editImageUrl.value.trim() || undefined;

    await editPost(props.id, {
      text,
      imageUrl: img,
    });

    emit('updated', {
      id: props.id,
      text,
      imageUrl: img,
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
  <article class="p-4 border-b border-neutral-800 hover:bg-neutral-950/40 transition-colors flex gap-3">
    <img :src="avatar" class="w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800" alt="Avatar" />

    <div class="flex flex-col gap-1.5 w-full">
      <div class="flex items-center justify-between text-sm">
        <div class="flex items-center gap-1.5">
          <span class="font-bold text-white hover:underline">{{ author }}</span>
          <span class="text-neutral-500">{{ handle }}</span>
          <span class="text-neutral-600">·</span>
          <span class="text-neutral-500 hover:underline">{{ timestamp }}</span>
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
          <button class="flex items-center gap-1.5 hover:text-sky-400 group transition-colors">
            <div class="p-2 rounded-full group-hover:bg-sky-500/10">
              <MessageCircle class="w-4 h-4" />
            </div>
            <span>{{ replies || 0 }}</span>
          </button>

          <button class="flex items-center gap-1.5 hover:text-emerald-400 group transition-colors">
            <div class="p-2 rounded-full group-hover:bg-emerald-500/10">
              <Repeat2 class="w-4 h-4" />
            </div>
            <span>{{ retweets || 0 }}</span>
          </button>

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
      </template>
    </div>
  </article>

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
</template>