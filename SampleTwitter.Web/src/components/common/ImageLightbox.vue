<script setup lang="ts">
import { ref, onBeforeUnmount, onMounted } from 'vue';
import { X } from 'lucide-vue-next';

const props = withDefaults(
  defineProps<{
    src: string;
    alt?: string;
    imgClass?: string | string[] | Record<string, boolean>;
  }>(),
  {
    alt: 'Image preview',
    imgClass: '',
  }
);

const isOpen = ref(false);

function openLightbox(event: MouseEvent) {
  if (!props.src) return;
  event.stopPropagation();
  isOpen.value = true;
}

function closeLightbox() {
  isOpen.value = false;
}

function handleKeyDown(event: KeyboardEvent) {
  if (event.key === 'Escape' && isOpen.value) {
    closeLightbox();
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown);
});

onBeforeUnmount(() => {
  isOpen.value = false;
  window.removeEventListener('keydown', handleKeyDown);
});
</script>

<template>
  <img
    :src="src"
    :alt="alt"
    :class="[imgClass, 'cursor-pointer hover:brightness-95 transition-all']"
    @click="openLightbox"
  />

  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-150 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-100 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="isOpen && src"
        class="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-8 bg-black/85 backdrop-blur-xs"
        @click.stop="closeLightbox"
      >
        <button
          type="button"
          @click.stop="closeLightbox"
          aria-label="Close full view"
          class="absolute top-4 right-4 p-2.5 rounded-full bg-neutral-900/90 hover:bg-neutral-800 text-white border border-neutral-700 transition-colors cursor-pointer shadow-lg"
        >
          <X class="w-5 h-5" />
        </button>

        <img
          :src="src"
          :alt="alt"
          class="max-w-[92vw] max-h-[90vh] w-auto h-auto object-contain rounded-xl shadow-2xl border border-neutral-800 bg-neutral-950"
          @click.stop
        />
      </div>
    </Transition>
  </Teleport>
</template>
