import { apiClient } from './client';
import type { PostFeedResponse, ReplyFeedResponse } from '@/types/api';

export async function getUserPosts(userId: number): Promise<PostFeedResponse> {
  const response = await apiClient.get<PostFeedResponse>(`/users/${userId}/posts`);
  return response.data;
}

export async function getUserReplies(userId: number): Promise<ReplyFeedResponse> {
  const response = await apiClient.get<ReplyFeedResponse>(`/users/${userId}/replies`);
  return response.data;
}