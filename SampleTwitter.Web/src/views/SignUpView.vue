<script setup lang="ts">
import { ref } from 'vue';
import { Twitter, Loader2, MailCheck } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';
import { signUp } from '@/api/account';
import { parseApiError } from '@/api/client';
import type { SignUpResponse } from '@/types/api';

const email = ref('');
const password = ref('');
const loading = ref(false);
const globalError = ref<string | null>(null);
const fieldErrors = ref<{ email?: string; password?: string }>({});
const successResponse = ref<SignUpResponse | null>(null);

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
  } else if (password.value.length < 8) {
    fieldErrors.value.password = 'The field Password must be a string or array type with a minimum length of \'8\'.';
    valid = false;
  }

  return valid;
}

async function handleSubmit() {
  globalError.value = null;
  if (!validateClient()) return;

  loading.value = true;
  try {
    const res = await signUp({
      email: email.value.trim(),
      password: password.value,
    });
    successResponse.value = res;
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

    if (parsed.status === 409) {
      globalError.value = parsed.detail || 'A confirmed account already exists for this email address.';
    } else if (!parsed.fieldErrors || Object.keys(parsed.fieldErrors).length === 0) {
      globalError.value = parsed.detail || parsed.message || 'An error occurred during registration.';
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
        <h1 class="text-2xl font-bold text-white tracking-tight">Create your account</h1>
      </div>

      <div v-if="successResponse" class="flex flex-col gap-5 items-center text-center py-4">
        <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
          <MailCheck class="w-8 h-8" />
        </div>
        <div class="flex flex-col gap-2">
          <h3 class="text-lg font-bold text-white">Check your email</h3>
          <p class="text-sm text-neutral-400 leading-relaxed">
            {{ successResponse.message }}
          </p>
        </div>
        <router-link
          to="/"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full text-center transition-colors text-sm"
        >
          Return to Home
        </router-link>
      </div>

      <form v-else @submit.prevent="handleSubmit" class="flex flex-col gap-4">
        <AlertBanner v-if="globalError" type="error" :message="globalError" />

        <InputField
          id="signup-email"
          label="Email"
          type="email"
          v-model="email"
          placeholder="name@example.com"
          :error="fieldErrors.email"
          :disabled="loading"
          required
        />

        <InputField
          id="signup-password"
          label="Password"
          type="password"
          v-model="password"
          placeholder="At least 8 characters"
          :error="fieldErrors.password"
          :disabled="loading"
          required
        />

        <button
          type="submit"
          :disabled="loading"
          class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-3 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 disabled:cursor-not-allowed text-sm"
        >
          <Loader2 v-if="loading" class="w-4 h-4 animate-spin" />
          <span>{{ loading ? 'Creating account...' : 'Sign up' }}</span>
        </button>

        <p class="text-center text-xs text-neutral-500 mt-2">
          Already have an account?
          <router-link to="/login" class="text-sky-400 hover:underline">Log in</router-link>
        </p>
      </form>
    </div>
  </div>
</template>
