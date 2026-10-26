import { apiClient } from './client';
import type { UploadImageResponse } from '@/types/api';

export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;
export const ALLOWED_IMAGE_ACCEPT = '.jpg,.jpeg,.png,.gif,.webp,image/jpeg,image/png,image/gif,image/webp';

const ALLOWED_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.gif', '.webp'];

export function validateImageFile(file: File): string | null {
  if (file.size === 0) {
    return 'The selected file is empty.';
  }

  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    return 'The image exceeds the maximum allowed size of 5 MB.';
  }

  const lowerName = file.name.toLowerCase();
  const hasAllowedExtension = ALLOWED_EXTENSIONS.some((ext) => lowerName.endsWith(ext));
  if (!hasAllowedExtension) {
    return 'Unsupported file format. Allowed formats: JPG, PNG, GIF, WEBP.';
  }

  return null;
}

export async function uploadImage(file: File): Promise<UploadImageResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<UploadImageResponse>('/images', formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return response.data;
}
