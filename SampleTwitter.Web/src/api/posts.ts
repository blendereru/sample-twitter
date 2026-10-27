import { apiClient } from './client';
import type { CreatePostRequest, CreatePostResponse, EditPostRequest, EditPostResponse, RepostResponse, BookmarkResponse, PostResponse } from '@/types/api';

export async function createPost(request: CreatePostRequest): Promise<CreatePostResponse> {
  const response = await apiClient.post<CreatePostResponse>('/posts', request);
  return response.data;
}

export async function editPost(id: number, request: EditPostRequest): Promise<EditPostResponse> {
  const response = await apiClient.put<EditPostResponse>(`/posts/${id}`, request);
  return response.data;
}

export async function deletePost(id: number): Promise<void> {
  await apiClient.delete(`/posts/${id}`);
}

export async function repostPost(id: number): Promise<RepostResponse> {
  const response = await apiClient.post<RepostResponse>(`/posts/${id}/repost`);
  return response.data;
}

export async function undoRepost(id: number): Promise<void> {
  await apiClient.delete(`/posts/${id}/repost`);
}

export const undoRepostPost = undoRepost;

export async function bookmarkPost(id: number): Promise<BookmarkResponse> {
  const response = await apiClient.post<BookmarkResponse>(`/posts/${id}/bookmark`);
  return response.data;
}

export async function undoBookmark(id: number): Promise<void> {
  await apiClient.delete(`/posts/${id}/bookmark`);
}

export const undoBookmarkPost = undoBookmark;

export async function getPostById(id: number): Promise<PostResponse> {
  const response = await apiClient.get<PostResponse>(`/posts/${id}`);
  return response.data;
}

export async function getPostReplies(id: number): Promise<PostResponse[]> {
  const response = await apiClient.get<PostResponse[]>(`/posts/${id}/replies`);
  return response.data;
}