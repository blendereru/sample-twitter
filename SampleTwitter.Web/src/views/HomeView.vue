<script setup lang="ts">
import { ref } from 'vue';
import TweetComposer from '@/components/tweet/TweetComposer.vue';
import TweetCard from '@/components/tweet/TweetCard.vue';
import { useAuthStore } from '@/stores/auth';

const authStore = useAuthStore();
const activeTab = ref<'forYou' | 'following'>('forYou');

interface TweetItem {
  id: number;
  userId?: number;
  author: string;
  handle: string;
  avatar: string;
  content: string;
  timestamp: string;
  imageUrl?: string;
  likes: number;
  retweets: number;
  replies: number;
}

const tweets = ref<TweetItem[]>([
  {
    id: -1,
    author: 'ASP.NET Core',
    handle: '@dotnet',
    avatar: 'https://images.unsplash.com/photo-1618401471353-b98afee0b2eb?w=80&auto=format&fit=crop&q=80',
    content: 'Welcome to SampleTwitter! Built with ASP.NET Core 10, PostgreSQL EF Core, Cookie-based Authentication, and Vue 3.',
    timestamp: '2h',
    likes: 42,
    retweets: 12,
    replies: 5
  },
  {
    id: -2,
    author: 'Vue.js',
    handle: '@vuejs',
    avatar: 'https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=80&auto=format&fit=crop&q=80',
    content: 'Vite + Vue 3 Composition API with TypeScript makes building full-stack reactive applications a breeze ✨',
    timestamp: '4h',
    likes: 88,
    retweets: 24,
    replies: 9
  }
]);

function isMyTweet(tweet: TweetItem): boolean {
  if (!authStore.isAuthenticated) return false;
  const currentId = authStore.currentUserId ?? (localStorage.getItem('sampletwitter_user_id') ? Number(localStorage.getItem('sampletwitter_user_id')) : null);
  const currentEmail = authStore.currentUserEmail || localStorage.getItem('sampletwitter_user_email');
  if (tweet.userId !== undefined && currentId !== null && Number(tweet.userId) === Number(currentId)) {
    return true;
  }
  if (currentEmail && tweet.handle === currentEmail) {
    return true;
  }
  return false;
}

function handleNewTweet(post: { id: number; text?: string; imageUrl?: string }) {
  const currentId = authStore.currentUserId ?? (localStorage.getItem('sampletwitter_user_id') ? Number(localStorage.getItem('sampletwitter_user_id')) : undefined);
  const currentEmail = authStore.currentUserEmail || localStorage.getItem('sampletwitter_user_email') || '@me';
  tweets.value.unshift({
    id: post.id,
    userId: currentId,
    author: `User #${currentId || 'Me'}`,
    handle: currentEmail,
    avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=80&auto=format&fit=crop&q=80',
    content: post.text || '',
    imageUrl: post.imageUrl,
    timestamp: 'Just now',
    likes: 0,
    retweets: 0,
    replies: 0
  });
}

function handleTweetUpdated(post: { id: number; text?: string; imageUrl?: string }) {
  const tweet = tweets.value.find(t => t.id === post.id);
  if (tweet) {
    tweet.content = post.text || '';
    tweet.imageUrl = post.imageUrl;
  }
}

function handleDeleteTweet(id: number) {
  tweets.value = tweets.value.filter(t => t.id !== id);
}
</script>

<template>
  <main class="flex flex-col min-h-screen border-r border-neutral-800 w-full max-w-2xl">
    <div class="sticky top-0 bg-black/80 backdrop-blur border-b border-neutral-800 z-10">
      <h2 class="text-xl font-bold p-4 pb-2 text-white">Home</h2>
      <div class="flex border-b border-neutral-800">
        <button
          @click="activeTab = 'forYou'"
          :class="[
            'flex-1 py-3.5 text-sm font-bold text-center hover:bg-neutral-900/60 transition-colors relative',
            activeTab === 'forYou' ? 'text-white' : 'text-neutral-500'
          ]"
        >
          For you
          <div v-if="activeTab === 'forYou'" class="absolute bottom-0 left-1/2 -translate-x-1/2 w-14 h-1 bg-sky-500 rounded-full"></div>
        </button>
        <button
          @click="activeTab = 'following'"
          :class="[
            'flex-1 py-3.5 text-sm font-bold text-center hover:bg-neutral-900/60 transition-colors relative',
            activeTab === 'following' ? 'text-white' : 'text-neutral-500'
          ]"
        >
          Following
          <div v-if="activeTab === 'following'" class="absolute bottom-0 left-1/2 -translate-x-1/2 w-16 h-1 bg-sky-500 rounded-full"></div>
        </button>
      </div>
    </div>

    <TweetComposer @post="handleNewTweet" />

    <div class="flex flex-col">
      <TweetCard
        v-for="tweet in tweets"
        :key="'tweet-' + tweet.id"
        :id="tweet.id"
        :user-id="tweet.userId"
        :can-edit="isMyTweet(tweet)"
        :author="tweet.author"
        :handle="tweet.handle"
        :avatar="tweet.avatar"
        :content="tweet.content"
        :image-url="tweet.imageUrl"
        :timestamp="tweet.timestamp"
        :likes="tweet.likes"
        :retweets="tweet.retweets"
        :replies="tweet.replies"
        @updated="handleTweetUpdated"
        @delete="handleDeleteTweet"
      />
    </div>
  </main>

</template>