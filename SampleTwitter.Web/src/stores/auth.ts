import { defineStore } from 'pinia';
import { ref } from 'vue';
import { getMe, signOut } from '@/api/account';
import type { MeResponse } from '@/types/api';

export const useAuthStore = defineStore('auth', () => {
  const storedUserId = localStorage.getItem('sampletwitter_user_id');
  const storedEmail = localStorage.getItem('sampletwitter_user_email');
  const storedUsername = localStorage.getItem('sampletwitter_user_username');
  const storedImageUrl = localStorage.getItem('sampletwitter_user_image_url');

  const isAuthenticated = ref<boolean>(!!storedUserId);
  const currentUserId = ref<number | null>(storedUserId ? Number(storedUserId) : null);
  const currentUserEmail = ref<string | null>(storedEmail || null);
  const currentUsername = ref<string | null>(storedUsername || null);
  const currentUserImageUrl = ref<string | null>(storedImageUrl || null);
  const registeredAt = ref<string | null>(null);
  const isInitializing = ref<boolean>(true);

  function setAuthenticated(
    userId: number,
    email?: string,
    userRegisteredAt?: string,
    username?: string | null,
    imageUrl?: string | null
  ) {
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
    if (username !== undefined) {
      currentUsername.value = username || null;
      if (username) {
        localStorage.setItem('sampletwitter_user_username', username);
      } else {
        localStorage.removeItem('sampletwitter_user_username');
      }
    }
    if (imageUrl !== undefined) {
      currentUserImageUrl.value = imageUrl || null;
      if (imageUrl) {
        localStorage.setItem('sampletwitter_user_image_url', imageUrl);
      } else {
        localStorage.removeItem('sampletwitter_user_image_url');
      }
    }
  }

  function updateProfileInfo(username?: string | null, imageUrl?: string | null) {
    currentUsername.value = username || null;
    currentUserImageUrl.value = imageUrl || null;
    if (username) {
      localStorage.setItem('sampletwitter_user_username', username);
    } else {
      localStorage.removeItem('sampletwitter_user_username');
    }
    if (imageUrl) {
      localStorage.setItem('sampletwitter_user_image_url', imageUrl);
    } else {
      localStorage.removeItem('sampletwitter_user_image_url');
    }
  }

  function clearAuth() {
    isAuthenticated.value = false;
    currentUserId.value = null;
    currentUserEmail.value = null;
    currentUsername.value = null;
    currentUserImageUrl.value = null;
    registeredAt.value = null;
    localStorage.removeItem('sampletwitter_user_id');
    localStorage.removeItem('sampletwitter_user_email');
    localStorage.removeItem('sampletwitter_user_username');
    localStorage.removeItem('sampletwitter_user_image_url');
  }

  async function logout(): Promise<void> {
    try {
      await signOut();
    } catch {
      // Even if the server request fails or session is already expired, clear local auth state.
    } finally {
      clearAuth();
    }
  }

  async function fetchCurrentUser(): Promise<MeResponse | null> {
    try {
      const user = await getMe();
      setAuthenticated(user.id, user.email, user.registeredAt, user.username, user.imageUrl);
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
    currentUsername,
    currentUserImageUrl,
    registeredAt,
    isInitializing,
    setAuthenticated,
    updateProfileInfo,
    clearAuth,
    logout,
    fetchCurrentUser,
  };
});
