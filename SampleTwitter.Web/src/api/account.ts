import { apiClient } from './client';
import type {
  SignUpRequest,
  SignUpResponse,
  ConfirmEmailResponse,
  ResendConfirmationEmailRequest,
  ResendConfirmationEmailResponse,
  LoginRequest,
  LoginResponse,
  MeResponse,
  ChangePasswordRequest,
  ChangePasswordResponse,
  ChangeEmailRequest,
  ChangeEmailResponse,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  ResetPasswordRequest,
  ResetPasswordResponse,
} from '@/types/api';

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

export async function resendConfirmationEmail(
  request: ResendConfirmationEmailRequest
): Promise<ResendConfirmationEmailResponse> {
  const response = await apiClient.post<ResendConfirmationEmailResponse>(
    '/auth/resend-confirmation-email',
    request
  );
  return response.data;
}

export async function getMe(): Promise<MeResponse> {
  const response = await apiClient.get<MeResponse>('/auth/me');
  return response.data;
}

export async function changePassword(request: ChangePasswordRequest): Promise<ChangePasswordResponse> {
  const response = await apiClient.put<ChangePasswordResponse>('/auth/password', request);
  return response.data;
}

export async function changeEmail(request: ChangeEmailRequest): Promise<ChangeEmailResponse> {
  const response = await apiClient.put<ChangeEmailResponse>('/auth/email', request);
  return response.data;
}

export async function forgotPassword(request: ForgotPasswordRequest): Promise<ForgotPasswordResponse> {
  const response = await apiClient.post<ForgotPasswordResponse>('/auth/forgot-password', request);
  return response.data;
}

export async function resetPassword(request: ResetPasswordRequest): Promise<ResetPasswordResponse> {
  const response = await apiClient.post<ResetPasswordResponse>('/auth/reset-password', request);
  return response.data;
}
