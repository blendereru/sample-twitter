import { defineStore } from 'pinia';
import { ref } from 'vue';
import { getMe } from '@/api/account';
import type { MeResponse } from '@/types/api';

export const useAuthStore = defineStore('auth', () => {
  const storedUserId = localStorage.getItem('sampletwitter_user_id');
  const storedEmail = localStorage.getItem('sampletwitter_user_email');

  const isAuthenticated = ref<boolean>(!!storedUserId);
  const currentUserId = ref<number | null>(storedUserId ? Number(storedUserId) : null);
  const currentUserEmail = ref<string | null>(storedEmail || null);
  const registeredAt = ref<string | null>(null);
  const isInitializing = ref<boolean>(true);

  function setAuthenticated(userId: number, email?: string, userRegisteredAt?: string) {
    isAuthenticated.value = true;
    currentUserId.value = userId;
    localStorage.setItem('sampletwitter_user_id', String(userId));
    if (email) {
      currentUserEmail.value = email;
      localStorage.setItem('sampletwitter_user_email', email);
    }
    if (userRegisteredAt) {
      registeredAt.value = userRegisteredAt;
    }
  }

  function clearAuth() {
    isAuthenticated.value = false;
    currentUserId.value = null;
    currentUserEmail.value = null;
    registeredAt.value = null;
    localStorage.removeItem('sampletwitter_user_id');
    localStorage.removeItem('sampletwitter_user_email');
  }

  async function fetchCurrentUser(): Promise<MeResponse | null> {
    try {
      const user = await getMe();
      setAuthenticated(user.id, user.email, user.registeredAt);
      return user;
    } catch {
      clearAuth();
      return null;
    } finally {
      isInitializing.value = false;
    }
  }

  return {
    isAuthenticated,
    currentUserId,
    currentUserEmail,
    registeredAt,
    isInitializing,
    setAuthenticated,
    clearAuth,
    fetchCurrentUser,
  };
});


