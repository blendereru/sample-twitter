<script setup lang="ts">
import { Heart, MessageCircle, Repeat2, Share, Bookmark } from 'lucide-vue-next';
import { ref } from 'vue';

const props = defineProps<{
  author: string;
  handle: string;
  avatar: string;
  content: string;
  timestamp: string;
  likes?: number;
  retweets?: number;
  replies?: number;
}>();

const liked = ref(false);
const likeCount = ref(props.likes || 0);

function toggleLike() {
  liked.value = !liked.value;
  likeCount.value += liked.value ? 1 : -1;
}
</script>

<template>
  <article class="p-4 border-b border-neutral-800 hover:bg-neutral-950/40 transition-colors flex gap-3 cursor-pointer">
    <img :src="avatar" class="w-10 h-10 rounded-full object-cover shrink-0 bg-neutral-800" alt="Avatar" />

    <div class="flex flex-col gap-1.5 w-full">
      <div class="flex items-center gap-1.5 text-sm">
        <span class="font-bold text-white hover:underline">{{ author }}</span>
        <span class="text-neutral-500">{{ handle }}</span>
        <span class="text-neutral-600">·</span>
        <span class="text-neutral-500 hover:underline">{{ timestamp }}</span>
      </div>

      <p class="text-sm leading-relaxed text-neutral-200 whitespace-pre-line">{{ content }}</p>

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
    </div>
  </article>
</template>
