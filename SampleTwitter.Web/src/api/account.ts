import { apiClient } from './client';
import type { SignUpRequest, SignUpResponse, ConfirmEmailResponse, LoginRequest, LoginResponse, MeResponse } from '@/types/api';

export async function signUp(request: SignUpRequest): Promise<SignUpResponse> {
  const response = await apiClient.post<SignUpResponse>('/auth/signup', request);
  return response.data;
}

export async function login(request: LoginRequest): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/auth/signin', request);
  return response.data;
}

export async function confirmEmail(userId: number, token: string): Promise<ConfirmEmailResponse> {
  const response = await apiClient.post<ConfirmEmailResponse>(
    '/auth/confirm-email',
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

export async function getMe(): Promise<MeResponse> {
  const response = await apiClient.get<MeResponse>('/auth/me');
  return response.data;
}

