<script setup lang="ts">
import { ref } from 'vue';
import { Twitter, Loader2, CheckCircle2, ArrowLeft } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';
import { forgotPassword } from '@/api/account';
import { parseApiError } from '@/api/client';

const email = ref('');
const loading = ref(false);
const submitted = ref(false);
const successMessage = ref('');
const globalError = ref<string | null>(null);
const fieldErrors = ref<{ email?: string }>({});

function validateClient(): boolean {
  fieldErrors.value = {};
  if (!email.value.trim()) {
    fieldErrors.value.email = 'The Email field is required.';
    return false;
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) {
    fieldErrors.value.email = 'The Email field is not a valid e-mail address.';
    return false;
  }
  return true;
}

async function handleSubmit() {
  globalError.value = null;
  if (!validateClient()) return;

  loading.value = true;
  try {
    const res = await forgotPassword({ email: email.value.trim() });
    submitted.value = true;
    successMessage.value =
      res.message || 'If a confirmed account with that email exists, a password reset link has been sent.';
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.Email?.length) {
      fieldErrors.value.email = parsed.fieldErrors.Email[0];
    } else {
      globalError.value = parsed.detail || parsed.message || 'An error occurred while requesting a password reset.';
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
        <h1 class="text-2xl font-bold text-white tracking-tight">Find your SampleTwitter account</h1>
        <p v-if="!submitted" class="text-xs text-neutral-400">
          Enter the email address associated with your account to receive a password reset link.
        </p>
      </div>

      <div v-if="submitted" class="flex flex-col items-center text-center gap-4 py-2">
        <div class="w-14 h-14 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
          <CheckCircle2 class="w-7 h-7" />
        </div>
        <h2 class="text-lg font-bold text-white">Check your email</h2>
        <p class="text-sm text-neutral-300 leading-relaxed">{{ successMessage }}</p>
        <router-link
          to="/login"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full text-center transition-colors text-sm"
        >
          Back to Sign in
        </router-link>
      </div>

      <form v-else @submit.prevent="handleSubmit" class="flex flex-col gap-4">
        <AlertBanner v-if="globalError" type="error" :message="globalError" />

        <InputField
          id="forgot-email"
          label="Email"
          type="email"
          v-model="email"
          placeholder="name@example.com"
          :error="fieldErrors.email"
          :disabled="loading"
          required
        />

        <button
          type="submit"
          :disabled="loading"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed text-sm cursor-pointer"
        >
          <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
          <span>{{ loading ? 'Sending reset link...' : 'Send reset link' }}</span>
        </button>

        <router-link
          to="/login"
          class="inline-flex items-center justify-center gap-1.5 text-xs text-neutral-400 hover:text-white mt-2 transition-colors"
        >
          <ArrowLeft class="w-3.5 h-3.5" />
          <span>Back to Sign in</span>
        </router-link>
      </form>
    </div>
  </div>
</template>
