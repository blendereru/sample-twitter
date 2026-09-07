import { apiClient } from './client';
import type { PostFeedResponse } from '@/types/api';

export async function getUserPosts(userId: number): Promise<PostFeedResponse> {
  const response = await apiClient.get<PostFeedResponse>(`/users/${userId}/posts`);
  return response.data;
}
