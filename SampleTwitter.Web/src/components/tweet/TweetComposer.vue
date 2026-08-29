<script setup lang="ts">
import { ref } from 'vue';
import { Image, Smile, Calendar, MapPin } from 'lucide-vue-next';
import { useAuthStore } from '@/stores/auth';

const emit = defineEmits<{
  (e: 'post', content: string): void;
}>();

const authStore = useAuthStore();
const content = ref('');

function handlePost() {
  if (!content.value.trim()) return;
  emit('post', content.value.trim());
  content.value = '';
}
</script>

<template>
  <div class="p-4 border-b border-neutral-800 flex gap-3">
    <div class="w-10 h-10 rounded-full bg-neutral-700 flex items-center justify-center font-bold text-sky-400 shrink-0">
      U
    </div>

    <div class="flex flex-col w-full gap-3">
      <textarea
        v-model="content"
        rows="3"
        placeholder="What is happening?!"
        class="w-full bg-transparent text-white text-lg placeholder-neutral-500 resize-none focus:outline-none"
      ></textarea>

      <div class="flex items-center justify-between pt-2 border-t border-neutral-800/80">
        <div class="flex items-center gap-1 text-sky-400">
          <button class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><Image class="w-4 h-4" /></button>
          <button class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><Smile class="w-4 h-4" /></button>
          <button class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><Calendar class="w-4 h-4" /></button>
          <button class="p-2 hover:bg-sky-500/10 rounded-full transition-colors"><MapPin class="w-4 h-4" /></button>
        </div>

        <button
          @click="handlePost"
          :disabled="!content.trim() || !authStore.isAuthenticated"
          :class="[
            'bg-sky-500 text-white font-bold px-4 py-1.5 rounded-full text-sm transition-opacity shadow-sm',
            !content.trim() || !authStore.isAuthenticated ? 'opacity-50 cursor-not-allowed' : 'hover:bg-sky-400'
          ]"
        >
          Post
        </button>
      </div>
    </div>
  </div>
</template>
