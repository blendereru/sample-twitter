<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { CheckCircle2, XCircle, Loader2 } from 'lucide-vue-next';
import { confirmEmail } from '@/api/account';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';

const route = useRoute();
const authStore = useAuthStore();

const loading = ref(true);
const success = ref(false);
const successMessage = ref('');
const errorMessage = ref('');

onMounted(async () => {
  const userIdParam = route.query.userId;
  const tokenParam = route.query.token;

  if (!userIdParam || !tokenParam) {
    loading.value = false;
    errorMessage.value = 'Invalid confirmation link. Missing userId or token.';
    return;
  }

  const userId = Number(userIdParam);
  const token = String(tokenParam);

  if (isNaN(userId)) {
    loading.value = false;
    errorMessage.value = 'Invalid user ID format in confirmation link.';
    return;
  }

  try {
    const res = await confirmEmail(userId, token);
    success.value = true;
    successMessage.value = res.message || 'Your email has been confirmed. You are now signed in.';
    await authStore.fetchCurrentUser();
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    errorMessage.value = parsed.detail || parsed.message || 'This confirmation link is invalid or has expired.';
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <div class="flex items-center justify-center min-h-[calc(100vh-2rem)] p-4 w-full">
    <div class="w-full max-w-md bg-neutral-950 border border-neutral-800 rounded-3xl p-8 shadow-2xl flex flex-col items-center text-center gap-6">
      <div v-if="loading" class="flex flex-col items-center gap-4 py-8">
        <Loader2 class="w-12 h-12 text-sky-400 animate-spin" />
        <h2 class="text-lg font-semibold text-white">Confirming your email...</h2>
        <p class="text-xs text-neutral-500">Please wait while we verify your token and sign you in.</p>
      </div>

      <div v-else-if="success" class="flex flex-col items-center gap-4 py-4 w-full">
        <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
          <CheckCircle2 class="w-8 h-8" />
        </div>
        <h2 class="text-2xl font-bold text-white">Email Confirmed!</h2>
        <p class="text-sm text-neutral-300 leading-relaxed">{{ successMessage }}</p>
        <router-link
          to="/"
          class="mt-4 w-full bg-sky-500 hover:bg-sky-400 text-white font-bold py-3 rounded-full text-center transition-colors text-sm"
        >
          Go to Home Feed
        </router-link>
      </div>

      <div v-else class="flex flex-col items-center gap-4 py-4 w-full">
        <div class="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/30 flex items-center justify-center text-red-400">
          <XCircle class="w-8 h-8" />
        </div>
        <h2 class="text-2xl font-bold text-white">Confirmation Failed</h2>
        <p class="text-sm text-red-300 leading-relaxed">{{ errorMessage }}</p>
        <div class="flex flex-col gap-2 w-full mt-4">
          <router-link
            to="/signup"
            class="w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full text-center transition-colors text-sm"
          >
            Register again
          </router-link>
          <router-link
            to="/"
            class="w-full border border-neutral-800 hover:bg-neutral-900 text-neutral-300 font-bold py-3 rounded-full text-center transition-colors text-sm"
          >
            Back to Home
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>
