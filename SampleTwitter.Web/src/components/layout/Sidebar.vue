<script setup lang="ts">
import { Home, Hash, Bell, Mail, Bookmark, User, Twitter, Feather, LogIn, UserPlus, LogOut } from 'lucide-vue-next';
import { useRoute, useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/auth';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const navItems = [
  { name: 'Home', path: '/', icon: Home },
  { name: 'Explore', path: '/#explore', icon: Hash },
  { name: 'Notifications', path: '/#notifications', icon: Bell },
  { name: 'Messages', path: '/#messages', icon: Mail },
  { name: 'Bookmarks', path: '/#bookmarks', icon: Bookmark },
  { name: 'Profile', path: '/#profile', icon: User },
];

function handleLogout() {
  authStore.clearAuth();
  router.push('/login');
}
</script>

<template>
  <header class="flex flex-col justify-between h-screen sticky top-0 px-3 py-4 select-none border-r border-neutral-800">
    <div class="flex flex-col gap-2">
      <router-link
        to="/"
        class="w-12 h-12 flex items-center justify-center rounded-full hover:bg-neutral-800/60 text-sky-400 transition-colors mb-2"
        aria-label="Twitter logo"
      >
        <Twitter class="w-8 h-8 fill-current" />
      </router-link>

      <nav class="flex flex-col gap-1">
        <router-link
          v-for="item in navItems"
          :key="item.name"
          :to="item.path"
          :class="[
            'flex items-center gap-4 px-4 py-3 rounded-full text-lg font-medium transition-colors hover:bg-neutral-800/80',
            route.path === item.path ? 'font-bold text-white' : 'text-neutral-300'
          ]"
        >
          <component :is="item.icon" class="w-6 h-6" />
          <span class="hidden xl:inline">{{ item.name }}</span>
        </router-link>
      </nav>

      <div class="mt-4">
        <router-link
          v-if="authStore.isAuthenticated"
          to="/"
          class="w-full bg-sky-500 hover:bg-sky-400 text-white font-bold py-3 px-6 rounded-full flex items-center justify-center gap-2 shadow-md transition-colors"
        >
          <Feather class="w-5 h-5 xl:hidden" />
          <span class="hidden xl:inline">Post</span>
        </router-link>

        <div v-else class="flex flex-col gap-2">
          <router-link
            to="/signup"
            class="w-full bg-white hover:bg-neutral-200 text-black font-bold py-2.5 px-5 rounded-full flex items-center justify-center gap-2 transition-colors text-center text-sm"
          >
            <UserPlus class="w-4 h-4 xl:hidden" />
            <span class="hidden xl:inline">Sign up</span>
          </router-link>
          <router-link
            to="/login"
            class="w-full border border-neutral-700 hover:bg-neutral-900 text-white font-bold py-2.5 px-5 rounded-full flex items-center justify-center gap-2 transition-colors text-center text-sm"
          >
            <LogIn class="w-4 h-4 xl:hidden" />
            <span class="hidden xl:inline">Log in</span>
          </router-link>
        </div>
      </div>
    </div>

    <div v-if="authStore.isAuthenticated" class="p-2 rounded-full hover:bg-neutral-800/60 flex items-center justify-between transition-colors">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-neutral-700 flex items-center justify-center font-bold text-sky-400 shrink-0">
          U
        </div>
        <div class="hidden xl:flex flex-col text-sm">
          <span class="font-bold text-white">User #{{ authStore.currentUserId }}</span>
          <span class="text-neutral-500 text-xs truncate max-w-[120px]">{{ authStore.currentUserEmail || 'Signed in' }}</span>
        </div>
      </div>
      <button
        @click="handleLogout"
        title="Sign out"
        class="p-2 hover:bg-red-500/10 rounded-full text-neutral-400 hover:text-red-400 transition-colors"
      >
        <LogOut class="w-4 h-4" />
      </button>
    </div>
  </header>
</template>

