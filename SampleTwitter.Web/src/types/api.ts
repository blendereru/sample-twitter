export interface SignUpRequest {
  email: string;
  password: string;
}

export interface SignUpResponse {
  userId: number;
  message: string;
}

export interface ConfirmEmailResponse {
  message: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  userId: number;
  email: string;
  message: string;
}

export interface MeResponse {
  id: number;
  email: string;
  registeredAt: string;
}

export interface CreatePostRequest {
  text?: string;
  imageUrl?: string;
  replyId?: number;
}

export interface CreatePostResponse {
  postId: number;
  message: string;
}

export interface EditPostRequest {
  text?: string;
  imageUrl?: string;
}

export interface EditPostResponse {
  id: number;
  text?: string;
  imageUrl?: string;
  updatedAt?: string;
}

export interface RepostResponse {
  postId: number;
  userId: number;
  createdAt: string;
}

export interface PostAuthorDto {
  id: number;
  email: string;
}

export interface PostFeedItemDto {
  id: number;
  text?: string;
  imageUrl?: string;
  createdAt: string;
  updatedAt?: string;
  author: PostAuthorDto;
  parentPost?: PostFeedItemDto;
  isRepost: boolean;
  repostedBy?: PostAuthorDto;
}

export interface PostFeedResponse {
  items: PostFeedItemDto[];
}


export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  requestId?: string;
  traceId?: string;
  timestamp?: string;
  errors?: Record<string, string[]>;
}

export interface ApiError {
  message: string;
  status?: number;
  title?: string;
  detail?: string;
  fieldErrors?: Record<string, string[]>;
}