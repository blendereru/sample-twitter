<script setup lang="ts">
import { ref, computed } from 'vue';
import { Image, Smile, Calendar, MapPin, Loader2, X } from 'lucide-vue-next';
import { useAuthStore } from '@/stores/auth';
import { createPost } from '@/api/posts';
import { parseApiError } from '@/api/client';

const emit = defineEmits<{
  (e: 'post', post: { id: number; text?: string; imageUrl?: string }): void;
}>();

const authStore = useAuthStore();
const content = ref('');
const imageUrl = ref('');
const showImageInput = ref(false);
const loading = ref(false);
const error = ref<string | null>(null);

const remainingChars = computed(() => 280 - content.value.length);
const isValid = computed(() => {
  const hasText = content.value.trim().length > 0;
  const hasImage = imageUrl.value.trim().length > 0;
  return (hasText || hasImage) && remainingChars.value >= 0;
});

async function handlePost() {
  if (!isValid.value || !authStore.isAuthenticated || loading.value) return;

  loading.value = true;
  error.value = null;

  try {
    const text = content.value.trim() || undefined;
    const img = imageUrl.value.trim() || undefined;

    const response = await createPost({
      text,
      imageUrl: img,
    });

    emit('post', {
      id: response.postId,
      text,
      imageUrl: img,
    });

    content.value = '';
    imageUrl.value = '';
    showImageInput.value = false;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.Text?.length) {
      error.value = parsed.fieldErrors.Text[0];
    } else if (parsed.fieldErrors?.ImageUrl?.length) {
      error.value = parsed.fieldErrors.ImageUrl[0];
    } else {
      error.value = parsed.detail || parsed.message || 'Failed to create post.';
    }
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="p-4 border-b border-neutral-800 flex flex-col gap-2">
    <div v-if="error" class="text-xs text-red-400 bg-red-950/40 border border-red-800/80 rounded-lg p-2.5">
      {{ error }}
    </div>

    <div class="flex gap-3">
      <div class="w-10 h-10 rounded-full bg-neutral-700 flex items-center justify-center font-bold text-sky-400 shrink-0">
        U
      </div>

      <div class="flex flex-col w-full gap-3">
        <textarea
          v-model="content"
          rows="3"
          placeholder="What is happening?!"
          :disabled="loading"
          class="w-full bg-transparent text-white text-lg placeholder-neutral-500 resize-none focus:outline-none disabled:opacity-50"
        ></textarea>

        <div v-if="showImageInput" class="flex items-center gap-2 bg-neutral-900 border border-neutral-800 rounded-xl p-2">
          <input
            v-model="imageUrl"
            type="url"
            placeholder="Enter image URL (e.g. https://...)"
            :disabled="loading"
            class="bg-transparent text-sm text-white placeholder-neutral-500 focus:outline-none w-full"
          />
          <button
            type="button"
            @click="showImageInput = false; imageUrl = ''"
            class="text-neutral-400 hover:text-white p-1 rounded-full hover:bg-neutral-800"
          >
            <X class="w-4 h-4" />
          </button>
        </div>

        <div v-if="imageUrl.trim()" class="relative rounded-xl overflow-hidden border border-neutral-800 max-h-48">
          <img :src="imageUrl" alt="Preview" class="w-full h-full object-cover" />
        </div>

        <div class="flex items-center justify-between pt-2 border-t border-neutral-800/80">
          <div class="flex items-center gap-1 text-sky-400">
            <button
              type="button"
              @click="showImageInput = !showImageInput"
              title="Add Image URL"
              :class="['p-2 hover:bg-sky-500/10 rounded-full transition-colors', showImageInput ? 'text-white bg-sky-500/20' : '']"
            >
              <Image class="w-4 h-4" />
            </button>
            <button type="button" class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><Smile class="w-4 h-4" /></button>
            <button type="button" class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><Calendar class="w-4 h-4" /></button>
            <button type="button" class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><MapPin class="w-4 h-4" /></button>
          </div>

          <div class="flex items-center gap-3">
            <span
              v-if="content.length > 0"
              :class="[
                'text-xs',
                remainingChars < 0 ? 'text-red-500 font-bold' : remainingChars <= 20 ? 'text-amber-400' : 'text-neutral-500'
              ]"
            >
              {{ remainingChars }}
            </span>

            <button
              @click="handlePost"
              :disabled="!isValid || !authStore.isAuthenticated || loading"
              :class="[
                'bg-sky-500 text-white font-bold px-4 py-1.5 rounded-full text-sm transition-opacity shadow-sm flex items-center gap-1.5',
                !isValid || !authStore.isAuthenticated || loading ? 'opacity-50 cursor-not-allowed' : 'hover:bg-sky-400'
              ]"
            >
              <Loader2 v-if="loading" class="w-3.5 h-3.5 animate-spin" />
              <span>Post</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

