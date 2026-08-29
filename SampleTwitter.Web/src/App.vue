<script setup lang="ts">
import Sidebar from '@/components/layout/Sidebar.vue';
import RightBar from '@/components/layout/RightBar.vue';
</script>

<template>
  <div class="min-h-screen bg-black text-[#e7e9ea] flex justify-center selection:bg-sky-500 selection:text-white">
    <div class="flex w-full max-w-7xl justify-between">
      <Sidebar class="w-20 xl:w-64 shrink-0" />
      <div class="flex-1 flex justify-center max-w-2xl">
        <router-view />
      </div>
      <RightBar class="shrink-0" />
    </div>
  </div>
</template>
