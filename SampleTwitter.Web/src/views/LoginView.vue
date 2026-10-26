<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { Twitter, Loader2, Mail } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';
import { login, resendConfirmationEmail } from '@/api/account';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';

const router = useRouter();
const authStore = useAuthStore();

const email = ref('');
const password = ref('');
const loading = ref(false);
const resendingConfirmation = ref(false);
const isUnconfirmedEmail = ref(false);
const resendSuccessMessage = ref<string | null>(null);
const globalError = ref<string | null>(null);
const fieldErrors = ref<{ email?: string; password?: string }>({});

function validateClient(): boolean {
  fieldErrors.value = {};
  let valid = true;

  if (!email.value.trim()) {
    fieldErrors.value.email = 'The Email field is required.';
    valid = false;
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
    fieldErrors.value.email = 'The Email field is not a valid e-mail address.';
    valid = false;
  }

  if (!password.value) {
    fieldErrors.value.password = 'The Password field is required.';
    valid = false;
  }

  return valid;
}

async function handleLogin() {
  globalError.value = null;
  resendSuccessMessage.value = null;
  isUnconfirmedEmail.value = false;
  if (!validateClient()) return;

  loading.value = true;
  try {
    const res = await login({
      email: email.value.trim(),
      password: password.value,
    });
    authStore.setAuthenticated(res.userId, res.email);
    await authStore.fetchCurrentUser();
    router.push('/');
  } catch (err: unknown) {
    const parsed = parseApiError(err);

    if (parsed.fieldErrors) {
      if (parsed.fieldErrors.Email?.length) {
        fieldErrors.value.email = parsed.fieldErrors.Email[0];
      }
      if (parsed.fieldErrors.Password?.length) {
        fieldErrors.value.password = parsed.fieldErrors.Password[0];
      }
    }

    if (parsed.status === 403) {
      isUnconfirmedEmail.value = true;
      globalError.value = parsed.detail || parsed.message || 'Please confirm your email address before signing in.';
    } else if (parsed.status === 401) {
      globalError.value = parsed.detail || parsed.message || 'Authentication failed.';
    } else if (!parsed.fieldErrors || Object.keys(parsed.fieldErrors).length === 0) {
      globalError.value = parsed.detail || parsed.message || 'An error occurred during sign in.';
    }
  } finally {
    loading.value = false;
  }
}

async function handleResendConfirmation() {
  const trimmedEmail = email.value.trim();
  if (!trimmedEmail || resendingConfirmation.value) return;

  resendingConfirmation.value = true;
  resendSuccessMessage.value = null;

  try {
    const res = await resendConfirmationEmail({ email: trimmedEmail });
    resendSuccessMessage.value =
      res.message || 'If an unconfirmed account with that email exists, a new confirmation link has been sent.';
    isUnconfirmedEmail.value = false;
    globalError.value = null;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    globalError.value = parsed.detail || parsed.message || 'Failed to resend confirmation email.';
  } finally {
    resendingConfirmation.value = false;
  }
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
        <AlertBanner v-if="globalError" type="error" :message="globalError" />
        <AlertBanner v-if="resendSuccessMessage" type="success" :message="resendSuccessMessage" />

        <div
          v-if="isUnconfirmedEmail"
          class="bg-neutral-900/70 border border-neutral-800 rounded-2xl p-3.5 flex flex-col gap-2.5 text-xs text-neutral-300"
        >
          <span>Didn't receive or lost your confirmation link?</span>
          <button
            type="button"
            @click="handleResendConfirmation"
            :disabled="resendingConfirmation"
            class="w-full py-2 px-3 rounded-full bg-sky-500/15 hover:bg-sky-500/25 border border-sky-500/30 text-sky-400 font-semibold flex items-center justify-center gap-1.5 transition-colors disabled:opacity-50 cursor-pointer"
          >
            <Loader2 v-if="resendingConfirmation" class="w-3.5 h-3.5 animate-spin" />
            <Mail v-else class="w-3.5 h-3.5" />
            <span>{{ resendingConfirmation ? 'Sending confirmation email...' : 'Resend confirmation email' }}</span>
          </button>
        </div>

        <InputField
          id="login-email"
          label="Email"
          type="email"
          v-model="email"
          placeholder="name@example.com"
          :error="fieldErrors.email"
          :disabled="loading"
          required
        />

        <div class="flex flex-col gap-1">
          <InputField
            id="login-password"
            label="Password"
            type="password"
            v-model="password"
            placeholder="Your password"
            :error="fieldErrors.password"
            :disabled="loading"
            required
          />
          <div class="flex justify-end">
            <router-link to="/forgot-password" class="text-xs text-sky-400 hover:underline">
              Forgot password?
            </router-link>
          </div>
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed text-sm"
        >
          <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
          <span>{{ loading ? 'Signing in...' : 'Log in' }}</span>
        </button>

        <p class="text-center text-xs text-neutral-500 mt-2">
          Don't have an account?
          <router-link to="/signup" class="text-sky-400 hover:underline">Sign up</router-link>
        </p>
      </form>
    </div>
  </div>
</template>
