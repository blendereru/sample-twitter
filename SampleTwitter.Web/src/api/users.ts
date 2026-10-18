import { apiClient } from './client';
import type {
  PostFeedResponse,
  ReplyFeedResponse,
  UserResponse,
  EditProfileRequest,
  EditProfileResponse,
} from '@/types/api';

export async function getUser(userId: number): Promise<UserResponse> {
  const response = await apiClient.get<UserResponse>(`/users/${userId}`);
  return response.data;
}

export async function editProfile(userId: number, request: EditProfileRequest): Promise<EditProfileResponse> {
  const response = await apiClient.put<EditProfileResponse>(`/users/${userId}`, request);
  return response.data;
}

export async function getUserPosts(userId: number): Promise<PostFeedResponse> {
  const response = await apiClient.get<PostFeedResponse>(`/users/${userId}/posts`);
  return response.data;
}

export async function getUserReplies(userId: number): Promise<ReplyFeedResponse> {
  const response = await apiClient.get<ReplyFeedResponse>(`/users/${userId}/replies`);
  return response.data;
}