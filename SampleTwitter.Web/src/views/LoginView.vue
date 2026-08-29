<script setup lang="ts">
import { ref } from 'vue';
import { Twitter } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';

const email = ref('');
const password = ref('');
const error = ref<string | null>(null);

function handleLogin() {
  error.value = 'Direct password login endpoint is in progress. Please sign up or confirm your email to sign in automatically.';
}
</script>

<template>
  <div class="flex items-center justify-center min-h-[calc(100vh-2rem)] p-4 w-full">
    <div class="w-full max-w-md bg-neutral-950 border border-neutral-800 rounded-3xl p-8 shadow-2xl flex flex-col gap-6">
      <div class="flex flex-col items-center gap-3 text-center">
        <Twitter class="w-10 h-10 text-white fill-current" />
        <h1 class="text-2xl font-bold text-white tracking-tight">Sign in to SampleTwitter</h1>
      </div>

      <form @submit.prevent="handleLogin" class="flex flex-col gap-4">
        <AlertBanner v-if="error" type="info" :message="error" />

        <InputField
          id="login-email"
          label="Email"
          type="email"
          v-model="email"
          placeholder="name@example.com"
          required
        />

        <InputField
          id="login-password"
          label="Password"
          type="password"
          v-model="password"
          placeholder="Your password"
          required
        />

        <button
          type="submit"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full flex items-center justify-center gap-2 transition-colors text-sm"
        >
          Log in
        </button>

        <p class="text-center text-xs text-neutral-500 mt-2">
          Don't have an account?
          <router-link to="/signup" class="text-sky-400 hover:underline">Sign up</router-link>
        </p>
      </form>
    </div>
  </div>
</template>
