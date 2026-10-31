<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { X, Loader2, User, Mail, Lock, CheckCircle2, Upload } from 'lucide-vue-next';
import InputField from '@/components/common/InputField.vue';
import AlertBanner from '@/components/common/AlertBanner.vue';
import ImageLightbox from '@/components/common/ImageLightbox.vue';
import { editProfile } from '@/api/users';
import { changeEmail, changePassword } from '@/api/account';
import { uploadImage, validateImageFile, ALLOWED_IMAGE_ACCEPT } from '@/api/images';
import { parseApiError } from '@/api/client';
import { useAuthStore } from '@/stores/auth';

const props = defineProps<{
  userId: number;
  initialUsername?: string | null;
  initialImageUrl?: string | null;
  currentEmail: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'profile-updated', payload: { username?: string | null; imageUrl?: string | null; updatedAt?: string | null }): void;
}>();

const router = useRouter();
const authStore = useAuthStore();

const activeSection = ref<'profile' | 'email' | 'password'>('profile');

// --- Profile Edit State ---
const username = ref(props.initialUsername ?? '');
const imageUrl = ref(props.initialImageUrl ?? '');
const profileLoading = ref(false);
const avatarUploading = ref(false);
const profileError = ref<string | null>(null);
const profileSuccess = ref<string | null>(null);
const profileFieldErrors = ref<{ username?: string }>({});
const avatarFileInputRef = ref<HTMLInputElement | null>(null);

function triggerAvatarFileInput() {
  if (profileLoading.value || avatarUploading.value) return;
  avatarFileInputRef.value?.click();
}

async function handleAvatarFileSelect(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;

  profileError.value = null;
  profileSuccess.value = null;

  const validationError = validateImageFile(file);
  if (validationError) {
    profileError.value = validationError;
    return;
  }

  avatarUploading.value = true;
  try {
    const res = await uploadImage(file);
    imageUrl.value = res.url;
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    profileError.value = parsed.detail || parsed.message || 'Failed to upload profile image.';
  } finally {
    avatarUploading.value = false;
  }
}

function validateProfile(): boolean {
  profileFieldErrors.value = {};
  profileError.value = null;

  const trimmedUsername = username.value.trim();
  const trimmedImage = imageUrl.value.trim();

  if (!trimmedUsername && !trimmedImage) {
    profileError.value = 'Please provide at least a display name or upload a profile photo.';
    return false;
  }

  if (trimmedUsername.length > 50) {
    profileFieldErrors.value.username = 'Username cannot exceed 50 characters.';
    return false;
  }

  return true;
}

async function handleSaveProfile() {
  profileSuccess.value = null;
  if (avatarUploading.value) return;
  if (!validateProfile()) return;

  profileLoading.value = true;
  try {
    const res = await editProfile(props.userId, {
      username: username.value.trim() ? username.value.trim() : null,
      imageUrl: imageUrl.value.trim() ? imageUrl.value.trim() : null,
    });

    authStore.updateProfileInfo(res.username, res.imageUrl);
    emit('profile-updated', {
      username: res.username,
      imageUrl: res.imageUrl,
      updatedAt: res.updatedAt,
    });
    profileSuccess.value = 'Profile updated successfully.';
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors?.Username?.length) {
      profileFieldErrors.value.username = parsed.fieldErrors.Username[0];
    } else if (parsed.fieldErrors?.ImageUrl?.length) {
      profileError.value = parsed.fieldErrors.ImageUrl[0];
    } else if (parsed.status === 409) {
      profileFieldErrors.value.username = parsed.detail || 'This username is already taken.';
    } else {
      profileError.value = parsed.detail || parsed.message || 'Failed to update profile.';
    }
  } finally {
    profileLoading.value = false;
  }
}

// --- Change Email State ---
const newEmail = ref('');
const updatedEmailAddress = ref('');
const emailCurrentPassword = ref('');
const emailLoading = ref(false);
const emailError = ref<string | null>(null);
const emailChangedSuccessMessage = ref<string | null>(null);
const emailFieldErrors = ref<{ newEmail?: string; currentPassword?: string }>({});

function validateEmailForm(): boolean {
  emailFieldErrors.value = {};
  emailError.value = null;
  let valid = true;

  if (!newEmail.value.trim()) {
    emailFieldErrors.value.newEmail = 'The NewEmail field is required.';
    valid = false;
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(newEmail.value.trim())) {
    emailFieldErrors.value.newEmail = 'The NewEmail field is not a valid e-mail address.';
    valid = false;
  }

  if (!emailCurrentPassword.value) {
    emailFieldErrors.value.currentPassword = 'The CurrentPassword field is required.';
    valid = false;
  }

  return valid;
}

async function handleSaveEmail() {
  if (!validateEmailForm()) return;

  emailLoading.value = true;
  try {
    const targetEmail = newEmail.value.trim();
    const res = await changeEmail({
      newEmail: targetEmail,
      currentPassword: emailCurrentPassword.value,
    });
    updatedEmailAddress.value = res.email || targetEmail;
    emailChangedSuccessMessage.value =
      res.message || 'Email updated. Please check your new email inbox and click the confirmation link to activate your new address.';
    emailCurrentPassword.value = '';
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors) {
      if (parsed.fieldErrors.NewEmail?.length) {
        emailFieldErrors.value.newEmail = parsed.fieldErrors.NewEmail[0];
      }
      if (parsed.fieldErrors.CurrentPassword?.length) {
        emailFieldErrors.value.currentPassword = parsed.fieldErrors.CurrentPassword[0];
      }
    }
    if (!parsed.fieldErrors || Object.keys(parsed.fieldErrors).length === 0) {
      emailError.value = parsed.detail || parsed.message || 'Failed to update email address.';
    }
  } finally {
    emailLoading.value = false;
  }
}

function handleCloseModal() {
  if (emailChangedSuccessMessage.value) {
    authStore.clearAuth();
    emit('close');
    router.push('/login');
    return;
  }
  emit('close');
}

function finishEmailChangeAndGoToLogin() {
  authStore.clearAuth();
  emit('close');
  router.push('/login');
}

// --- Change Password State ---
const currentPassword = ref('');
const newPassword = ref('');
const confirmNewPassword = ref('');
const passwordLoading = ref(false);
const passwordError = ref<string | null>(null);
const passwordSuccess = ref<string | null>(null);
const passwordFieldErrors = ref<{
  currentPassword?: string;
  newPassword?: string;
  confirmNewPassword?: string;
}>({});

function validatePasswordForm(): boolean {
  passwordFieldErrors.value = {};
  passwordError.value = null;
  let valid = true;

  if (!currentPassword.value) {
    passwordFieldErrors.value.currentPassword = 'The CurrentPassword field is required.';
    valid = false;
  }

  if (!newPassword.value) {
    passwordFieldErrors.value.newPassword = 'The NewPassword field is required.';
    valid = false;
  } else if (newPassword.value.length < 8) {
    passwordFieldErrors.value.newPassword = 'The new password must be at least 8 characters long.';
    valid = false;
  }

  if (confirmNewPassword.value !== newPassword.value) {
    passwordFieldErrors.value.confirmNewPassword = 'Passwords do not match.';
    valid = false;
  }

  return valid;
}

async function handleSavePassword() {
  passwordSuccess.value = null;
  if (!validatePasswordForm()) return;

  passwordLoading.value = true;
  try {
    const res = await changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    });
    passwordSuccess.value = res.message || 'Password updated successfully.';
    currentPassword.value = '';
    newPassword.value = '';
    confirmNewPassword.value = '';
  } catch (err: unknown) {
    const parsed = parseApiError(err);
    if (parsed.fieldErrors) {
      if (parsed.fieldErrors.CurrentPassword?.length) {
        passwordFieldErrors.value.currentPassword = parsed.fieldErrors.CurrentPassword[0];
      }
      if (parsed.fieldErrors.NewPassword?.length) {
        passwordFieldErrors.value.newPassword = parsed.fieldErrors.NewPassword[0];
      }
    }
    if (!parsed.fieldErrors || Object.keys(parsed.fieldErrors).length === 0) {
      passwordError.value = parsed.detail || parsed.message || 'Failed to update password.';
    }
  } finally {
    passwordLoading.value = false;
  }
}
</script>

<template>
  <div
    class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4"
    @click.self="handleCloseModal"
  >
    <div class="w-full max-w-lg bg-neutral-950 border border-neutral-800 rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
      <!-- Modal Header -->
      <div class="flex items-center justify-between px-5 py-4 border-b border-neutral-800">
        <div class="flex items-center gap-3">
          <button
            @click="handleCloseModal"
            class="p-1.5 rounded-full hover:bg-neutral-800 text-neutral-400 hover:text-white transition-colors cursor-pointer"
            aria-label="Close modal"
          >
            <X class="w-5 h-5" />
          </button>
          <h2 class="text-lg font-bold text-white">Account & Profile Settings</h2>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="flex border-b border-neutral-800 text-xs sm:text-sm font-semibold">
        <button
          @click="activeSection = 'profile'"
          :class="[
            'flex-1 py-3 flex items-center justify-center gap-2 transition-colors relative cursor-pointer',
            activeSection === 'profile' ? 'text-white' : 'text-neutral-500 hover:text-neutral-300'
          ]"
        >
          <User class="w-4 h-4" />
          <span>Profile</span>
          <div
            v-if="activeSection === 'profile'"
            class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
          ></div>
        </button>

        <button
          @click="activeSection = 'email'"
          :class="[
            'flex-1 py-3 flex items-center justify-center gap-2 transition-colors relative cursor-pointer',
            activeSection === 'email' ? 'text-white' : 'text-neutral-500 hover:text-neutral-300'
          ]"
        >
          <Mail class="w-4 h-4" />
          <span>Email</span>
          <div
            v-if="activeSection === 'email'"
            class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
          ></div>
        </button>

        <button
          @click="activeSection = 'password'"
          :class="[
            'flex-1 py-3 flex items-center justify-center gap-2 transition-colors relative cursor-pointer',
            activeSection === 'password' ? 'text-white' : 'text-neutral-500 hover:text-neutral-300'
          ]"
        >
          <Lock class="w-4 h-4" />
          <span>Password</span>
          <div
            v-if="activeSection === 'password'"
            class="absolute bottom-0 left-1/2 -translate-x-1/2 w-12 h-1 bg-sky-500 rounded-full"
          ></div>
        </button>
      </div>

      <!-- Modal Body -->
      <div class="p-6 overflow-y-auto">
        <!-- 1. Edit Profile Section -->
        <form v-if="activeSection === 'profile'" @submit.prevent="handleSaveProfile" class="flex flex-col gap-4">
          <AlertBanner v-if="profileError" type="error" :message="profileError" />
          <AlertBanner v-if="profileSuccess" type="success" :message="profileSuccess" />

          <!-- Avatar Preview & Upload -->
          <div class="flex items-center justify-between gap-4 pb-2">
            <div class="flex items-center gap-4">
              <ImageLightbox
                v-if="imageUrl.trim()"
                :src="imageUrl.trim()"
                alt="Avatar preview"
                img-class="w-16 h-16 rounded-full object-cover border-2 border-neutral-700 bg-neutral-900 shrink-0"
              />
              <div
                v-else
                class="w-16 h-16 rounded-full bg-neutral-800 border-2 border-neutral-700 flex items-center justify-center text-xl font-bold text-sky-400 shrink-0 uppercase"
              >
                {{ (username.trim() || currentEmail || 'U')[0] }}
              </div>
              <div class="flex flex-col text-xs text-neutral-400">
                <span class="font-semibold text-neutral-200">Profile Photo</span>
                <span>JPG, PNG, GIF, or WEBP (max 5 MB).</span>
              </div>
            </div>

            <div class="flex items-center gap-2 shrink-0">
              <input
                ref="avatarFileInputRef"
                type="file"
                :accept="ALLOWED_IMAGE_ACCEPT"
                class="hidden"
                @change="handleAvatarFileSelect"
              />
              <button
                type="button"
                @click="triggerAvatarFileInput"
                :disabled="profileLoading || avatarUploading"
                class="px-3.5 py-2 rounded-full border border-neutral-700 hover:bg-neutral-900 text-xs font-semibold text-white flex items-center gap-1.5 transition-colors disabled:opacity-50 cursor-pointer"
              >
                <Loader2 v-if="avatarUploading" class="w-3.5 h-3.5 animate-spin text-sky-400" />
                <Upload v-else class="w-3.5 h-3.5 text-sky-400" />
                <span>{{ avatarUploading ? 'Uploading...' : 'Upload photo' }}</span>
              </button>
            </div>
          </div>

          <div class="flex flex-col gap-1">
            <InputField
              id="edit-profile-username"
              label="Display Name"
              type="text"
              v-model="username"
              placeholder="Your display name (max 50 characters)"
              :error="profileFieldErrors.username"
              :disabled="profileLoading"
            />
            <div class="flex justify-end text-xs text-neutral-500">
              {{ username.trim().length }} / 50
            </div>
          </div>

          <button
            type="submit"
            :disabled="profileLoading || avatarUploading"
            class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-2.5 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 text-sm cursor-pointer"
          >
            <Loader2 v-if="profileLoading" class="w-4 h-4 animate-spin" />
            <span>{{ profileLoading ? 'Saving...' : 'Save profile' }}</span>
          </button>
        </form>

        <!-- 2. Change Email Section -->
        <div v-else-if="activeSection === 'email'">
          <div v-if="emailChangedSuccessMessage" class="flex flex-col items-center text-center gap-4 py-4">
            <div class="w-14 h-14 rounded-full bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
              <CheckCircle2 class="w-7 h-7" />
            </div>
            <h3 class="text-lg font-bold text-white">Confirmation email sent!</h3>
            <AlertBanner
              type="success"
              :message="emailChangedSuccessMessage"
            />
            <p class="text-xs text-neutral-400 leading-relaxed">
              We sent a confirmation link to <strong class="text-white">{{ updatedEmailAddress }}</strong>.
              Please open your email and click the confirmation link to verify your new address and sign back in.
            </p>
            <button
              @click="finishEmailChangeAndGoToLogin"
              class="mt-2 w-full bg-sky-500 hover:bg-sky-400 text-white font-bold py-2.5 rounded-full text-sm transition-colors cursor-pointer"
            >
              Got it, go to Sign in
            </button>
          </div>

          <form v-else @submit.prevent="handleSaveEmail" class="flex flex-col gap-4">
            <AlertBanner v-if="emailError" type="error" :message="emailError" />

            <div class="bg-neutral-900/70 border border-neutral-800 rounded-2xl p-3.5 text-xs text-neutral-400 flex flex-col gap-1">
              <span>Current email: <strong class="text-white">{{ currentEmail }}</strong></span>
              <span>Changing your email will send a verification link to your new address and sign you out until confirmed.</span>
            </div>

            <InputField
              id="change-email-new"
              label="New email address"
              type="email"
              v-model="newEmail"
              placeholder="new-email@example.com"
              :error="emailFieldErrors.newEmail"
              :disabled="emailLoading"
              required
            />

            <InputField
              id="change-email-password"
              label="Current password"
              type="password"
              v-model="emailCurrentPassword"
              placeholder="Verify your current password"
              :error="emailFieldErrors.currentPassword"
              :disabled="emailLoading"
              required
            />

            <button
              type="submit"
              :disabled="emailLoading"
              class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-2.5 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 text-sm cursor-pointer"
            >
              <Loader2 v-if="emailLoading" class="w-4 h-4 animate-spin" />
              <span>{{ emailLoading ? 'Updating email...' : 'Update email' }}</span>
            </button>
          </form>
        </div>

        <!-- 3. Change Password Section -->
        <form v-else-if="activeSection === 'password'" @submit.prevent="handleSavePassword" class="flex flex-col gap-4">
          <AlertBanner v-if="passwordError" type="error" :message="passwordError" />
          <AlertBanner v-if="passwordSuccess" type="success" :message="passwordSuccess" />

          <InputField
            id="change-pw-current"
            label="Current password"
            type="password"
            v-model="currentPassword"
            placeholder="Enter current password"
            :error="passwordFieldErrors.currentPassword"
            :disabled="passwordLoading"
            required
          />

          <InputField
            id="change-pw-new"
            label="New password"
            type="password"
            v-model="newPassword"
            placeholder="At least 8 characters"
            :error="passwordFieldErrors.newPassword"
            :disabled="passwordLoading"
            required
          />

          <InputField
            id="change-pw-confirm"
            label="Confirm new password"
            type="password"
            v-model="confirmNewPassword"
            placeholder="Re-enter new password"
            :error="passwordFieldErrors.confirmNewPassword"
            :disabled="passwordLoading"
            required
          />

          <button
            type="submit"
            :disabled="passwordLoading"
            class="mt-2 w-full bg-white hover:bg-neutral-200 text-black font-bold py-2.5 rounded-full flex items-center justify-center gap-2 transition-colors disabled:opacity-50 text-sm cursor-pointer"
          >
            <Loader2 v-if="passwordLoading" class="w-4 h-4 animate-spin" />
            <span>{{ passwordLoading ? 'Updating password...' : 'Update password' }}</span>
          </button>
        </form>
      </div>
    </div>
  </div>
</template>