// Mirrors the server-side rules in SampleTwitter.API/Models/User.cs (GetUsernameValidationError).
// The server remains the source of truth; this only provides instant feedback in forms.

export const USERNAME_MIN_LENGTH = 3;
export const USERNAME_MAX_LENGTH = 30;

const USERNAME_ALLOWED_CHARACTERS = /^[a-zA-Z0-9_]+$/;

const RESERVED_USERNAMES = new Set([
  'admin', 'administrator', 'api', 'auth', 'bookmarks', 'explore', 'help', 'home', 'login', 'logout',
  'me', 'messages', 'moderator', 'notifications', 'null', 'official', 'profile', 'root', 'search',
  'settings', 'signin', 'signup', 'staff', 'support', 'system', 'undefined',
]);

/** Returns an error message for an invalid username, or null when it is valid. */
export function validateUsername(value: string): string | null {
  const trimmed = value.trim();

  if (!trimmed) {
    return 'Username is required.';
  }
  if (trimmed.length < USERNAME_MIN_LENGTH || trimmed.length > USERNAME_MAX_LENGTH) {
    return `Username must be between ${USERNAME_MIN_LENGTH} and ${USERNAME_MAX_LENGTH} characters.`;
  }
  if (!USERNAME_ALLOWED_CHARACTERS.test(trimmed)) {
    return 'Username can only contain letters, digits and underscores.';
  }
  if (RESERVED_USERNAMES.has(trimmed.toLowerCase())) {
    return 'This username is reserved.';
  }
  return null;
}