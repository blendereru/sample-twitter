import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const isAuthenticated = ref<boolean>(false);
  const currentUserId = ref<number | null>(null);
  const currentUserEmail = ref<string | null>(null);

  function setAuthenticated(userId: number, email?: string) {
    isAuthenticated.value = true;
    currentUserId.value = userId;
    if (email) currentUserEmail.value = email;
  }

  function clearAuth() {
    isAuthenticated.value = false;
    currentUserId.value = null;
    currentUserEmail.value = null;
  }

  return {
    isAuthenticated,
    currentUserId,
    currentUserEmail,
    setAuthenticated,
    clearAuth,
  };
});
