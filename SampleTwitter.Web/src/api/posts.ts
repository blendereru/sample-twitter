import { apiClient } from './client';
import type { CreatePostRequest, CreatePostResponse } from '@/types/api';

export async function createPost(request: CreatePostRequest): Promise<CreatePostResponse> {
  const response = await apiClient.post<CreatePostResponse>('/posts', request);
  return response.data;
}