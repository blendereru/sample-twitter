<script setup lang="ts">
defineProps<{
  id: string;
  label: string;
  type?: string;
  modelValue: string;
  placeholder?: string;
  error?: string;
  disabled?: boolean;
  required?: boolean;
}>();

defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();
</script>

<template>
  <div class="flex flex-col gap-1.5 w-full">
    <label :for="id" class="text-sm font-medium text-neutral-300">
      {{ label }}
      <span v-if="required" class="text-red-400">*</span>
    </label>
    <input
      :id="id"
      :type="type || 'text'"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      :class="[
        'w-full px-4 py-3 rounded-lg bg-neutral-900 border text-white placeholder-neutral-500 focus:outline-none transition-colors text-base',
        error
          ? 'border-red-500 focus:border-red-500 focus:ring-1 focus:ring-red-500'
          : 'border-neutral-800 focus:border-sky-500 focus:ring-1 focus:ring-sky-500',
        disabled ? 'opacity-50 cursor-not-allowed' : ''
      ]"
    />
    <p v-if="error" class="text-xs text-red-400 mt-0.5">{{ error }}</p>
  </div>
</template>
