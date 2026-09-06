import { apiClient } from './client';
import type { CreatePostRequest, CreatePostResponse, EditPostRequest, EditPostResponse } from '@/types/api';

export async function createPost(request: CreatePostRequest): Promise<CreatePostResponse> {
  const response = await apiClient.post<CreatePostResponse>('/posts', request);
  return response.data;
}

export async function editPost(id: number, request: EditPostRequest): Promise<EditPostResponse> {
  const response = await apiClient.put<EditPostResponse>(`/posts/${id}`, request);
  return response.data;
}