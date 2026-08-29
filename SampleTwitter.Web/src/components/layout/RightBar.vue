<script setup lang="ts">
import { Search } from 'lucide-vue-next';

const trends = [
  { category: 'Technology · Trending', topic: '#DotNet10', posts: '42.5K posts' },
  { category: 'Web Development · Trending', topic: '#VueJS', posts: '89.1K posts' },
  { category: 'Software Engineering', topic: 'PostgreSQL', posts: '15.3K posts' },
  { category: 'Tech · Trending', topic: 'Tailwind CSS', posts: '31.8K posts' },
];

const whoToFollow = [
  { name: 'ASP.NET Core', handle: '@dotnet', avatar: 'https://images.unsplash.com/photo-1618401471353-b98afee0b2eb?w=80&auto=format&fit=crop&q=80' },
  { name: 'Vue.js', handle: '@vuejs', avatar: 'https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=80&auto=format&fit=crop&q=80' },
];
</script>

<template>
  <aside class="hidden lg:flex flex-col gap-4 p-4 min-w-[320px] max-w-[380px] border-l border-neutral-800">
    <div class="sticky top-0 bg-black/80 backdrop-blur pb-2 z-10">
      <div class="flex items-center gap-3 px-4 py-2.5 rounded-full bg-neutral-900 border border-neutral-800 text-neutral-400 focus-within:border-sky-500 focus-within:text-sky-400">
        <Search class="w-4 h-4 shrink-0" />
        <input
          type="text"
          placeholder="Search"
          class="bg-transparent text-sm text-white placeholder-neutral-500 focus:outline-none w-full"
        />
      </div>
    </div>

    <div class="bg-neutral-900/70 border border-neutral-800 rounded-2xl p-4 flex flex-col gap-3">
      <h3 class="text-xl font-bold text-white">What's happening</h3>
      <div class="flex flex-col gap-3 divide-y divide-neutral-800/60">
        <div
          v-for="(trend, i) in trends"
          :key="i"
          class="pt-3 first:pt-0 cursor-pointer hover:bg-neutral-800/40 p-2 -mx-2 rounded-xl transition-colors"
        >
          <span class="text-xs text-neutral-500">{{ trend.category }}</span>
          <h4 class="font-bold text-sm text-white mt-0.5">{{ trend.topic }}</h4>
          <span class="text-xs text-neutral-500">{{ trend.posts }}</span>
        </div>
      </div>
    </div>

    <div class="bg-neutral-900/70 border border-neutral-800 rounded-2xl p-4 flex flex-col gap-3">
      <h3 class="text-xl font-bold text-white">Who to follow</h3>
      <div class="flex flex-col gap-3 divide-y divide-neutral-800/60">
        <div
          v-for="(person, i) in whoToFollow"
          :key="i"
          class="pt-3 first:pt-0 flex items-center justify-between"
        >
          <div class="flex items-center gap-2.5">
            <img :src="person.avatar" class="w-10 h-10 rounded-full object-cover bg-neutral-800" alt="Avatar" />
            <div class="flex flex-col">
              <span class="font-bold text-sm text-white hover:underline cursor-pointer">{{ person.name }}</span>
              <span class="text-xs text-neutral-500">{{ person.handle }}</span>
            </div>
          </div>
          <button class="bg-white hover:bg-neutral-200 text-black font-semibold text-xs px-4 py-1.5 rounded-full transition-colors">
            Follow
          </button>
        </div>
      </div>
    </div>
  </aside>
</template>
