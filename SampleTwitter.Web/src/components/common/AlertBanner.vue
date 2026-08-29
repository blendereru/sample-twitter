<script setup lang="ts">
import { AlertCircle, CheckCircle2, Info } from 'lucide-vue-next';

defineProps<{
  type: 'error' | 'success' | 'info';
  title?: string;
  message?: string;
}>();
</script>

<template>
  <div
    :class="[
      'p-4 rounded-xl border flex items-start gap-3 text-sm',
      type === 'error' ? 'bg-red-950/40 border-red-800/80 text-red-200' : '',
      type === 'success' ? 'bg-emerald-950/40 border-emerald-800/80 text-emerald-200' : '',
      type === 'info' ? 'bg-sky-950/40 border-sky-800/80 text-sky-200' : ''
    ]"
  >
    <AlertCircle v-if="type === 'error'" class="w-5 h-5 text-red-400 shrink-0 mt-0.5" />
    <CheckCircle2 v-else-if="type === 'success'" class="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" />
    <Info v-else class="w-5 h-5 text-sky-400 shrink-0 mt-0.5" />

    <div class="flex flex-col gap-1">
      <h4 v-if="title" class="font-semibold">{{ title }}</h4>
      <p v-if="message" class="leading-relaxed opacity-90">{{ message }}</p>
      <slot />
    </div>
  </div>
</template>
