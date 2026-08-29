import { apiClient } from './client';
import type { SignUpRequest, SignUpResponse, ConfirmEmailResponse } from '@/types/api';

export async function signUp(request: SignUpRequest): Promise<SignUpResponse> {
  const response = await apiClient.post<SignUpResponse>('/account/signup', request);
  return response.data;
}

export async function confirmEmail(userId: number, token: string): Promise<ConfirmEmailResponse> {
  const response = await apiClient.post<ConfirmEmailResponse>(
    '/account/confirm-email',
    null,
    {
      params: {
        userId,
        token,
      },
    }
  );
  return response.data;
}
