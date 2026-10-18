<script setup lang="ts">
import { ref, computed } from 'vue';
import { useRoute } from 'vue-router';
import { Twitter, Loader2, CheckCircle2, XCircle } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';
import { resetPassword } from '@/api/account';
import { parseApiError } from '@/api/client';

const route = useRoute();

const userId = computed(() => {
  const param = route.query.userId;
  return param ? Number(param) : NaN;
});

const token = computed(() => {
  const param = route.query.token;
  return param ? String(param) : '';
});

const hasValidLinkParams = computed(() => {
  return !isNaN(userId.value) && userId.value > 0 && token.value.trim().length > 0;
});

const newPassword = ref('');
const confirmPassword = ref('');
const loading = ref(false);
const success = ref(false);
const successMessage = ref('');
const globalError = ref<string | null>(null);
const fieldErrors = ref<{ newPassword?: string; confirmPassword?: string }>({});

function validateClient(): boolean {
  fieldErrors.value = {};
  let valid = true;

  if (!newPassword.value) {
    fieldErrors.value.newPassword = 'The NewPassword field is required.';
    valid = false;
  } else if (newPassword.value.length < 8) {
    fieldErrors.value.newPassword = 'The password must be at least 8 characters long.';
    valid = false;
  }

  if (confirmPassword.value !== newPassword.value) {
    fieldErrors.value.confirmPassword = 'Passwords do not match.';
    valid = false;
  }

  return valid;
}

async function handleReset() {
  globalError.value = null;
  if (!validateClient()) return;

  loading.value = true;
  try {
    const res = await resetPassword({
      userId: userId.value,
      token: token.value,
      newPassword: newPassword.value,
    });
    success.value = true;
    successMessage.value = res.message || 'Your password has been reset successfully. You can now sign in.';
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.NewPassword?.length) {
      fieldErrors.value.newPassword = parsed.fieldErrors.NewPassword[0];
    } else if (parsed.fieldErrors?.Token?.length) {
      globalError.value = parsed.fieldErrors.Token[0];
    } else {
      globalError.value = parsed.detail || parsed.message || 'This password reset link is invalid or has expired.';
    }
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <div class="flex items-center justify-center min-h-[calc(100vh-2rem)] p-4 w-full">
    <div class="w-full max-w-md bg-neutral-950 border border-neutral-800 rounded-3xl p-8 shadow-2xl flex flex-col gap-6">
      <div class="flex flex-col items-center gap-3 text-center">
        <Twitter class="w-10 h-10 text-white fill-current" />
        <h1 class="text-2xl font-bold text-white tracking-tight">Choose a new password</h1>
      </div>

      <!-- Missing query params state -->
      <div v-if="!hasValidLinkParams" class="flex flex-col items-center text-center gap-4 py-4">
        <div class="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/30 flex items-center justify-center text-red-400">
          <XCircle class="w-8 h-8" />
        </div>
        <h2 class="text-xl font-bold text-white">Invalid Reset Link</h2>
        <p class="text-sm text-red-300 leading-relaxed">
          This password reset link is missing required parameters. Please request a new password reset link.
        </p>
        <router-link
          to="/forgot-password"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full text-center transition-colors text-sm"
        >
          Request new link
        </router-link>
      </div>

      <!-- Success state -->
      <div v-else-if="success" class="flex flex-col items-center text-center gap-4 py-4">
        <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
          <CheckCircle2 class="w-8 h-8" />
        </div>
        <h2 class="text-xl font-bold text-white">Password Reset!</h2>
        <p class="text-sm text-neutral-300 leading-relaxed">{{ successMessage }}</p>
        <router-link
          to="/login"
          class="mt-2 w-full bg-sky-500 hover:bg-sky-400 text-white font-bold py-3 rounded-full text-center transition-colors text-sm"
        >
          Sign in with new password
        </router-link>
      </div>

      <!-- Form state -->
      <form v-else @submit.prevent="handleReset" class="flex flex-col gap-4">
        <AlertBanner v-if="globalError" type="error" :message="globalError" />

        <InputField
          id="reset-new-password"
          label="New password"
          type="password"
          v-model="newPassword"
          placeholder="At least 8 characters"
          :error="fieldErrors.newPassword"
          :disabled="loading"
          required
        />

        <InputField
          id="reset-confirm-password"
          label="Confirm new password"
          type="password"
          v-model="confirmPassword"
          placeholder="Re-enter your new password"
          :error="fieldErrors.confirmPassword"
          :disabled="loading"
          required
        />

        <button
          type="submit"
          :disabled="loading"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed text-sm cursor-pointer"
        >
          <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
          <span>{{ loading ? 'Resetting password...' : 'Reset password' }}</span>
        </button>
      </form>
    </div>
  </div>
</template>
