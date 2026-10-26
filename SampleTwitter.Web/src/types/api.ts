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
  username?: string | null;
  imageUrl?: string | null;
  updatedAt?: string | null;
}

export interface UserResponse {
  id: number;
  email: string;
  registeredAt: string;
  username?: string | null;
  imageUrl?: string | null;
  updatedAt?: string | null;
}

export interface EditProfileRequest {
  username?: string | null;
  imageUrl?: string | null;
}

export interface EditProfileResponse {
  id: number;
  username?: string | null;
  imageUrl?: string | null;
  updatedAt?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ChangePasswordResponse {
  message: string;
}

export interface ChangeEmailRequest {
  newEmail: string;
  currentPassword: string;
}

export interface ChangeEmailResponse {
  userId: number;
  email: string;
  message: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ForgotPasswordResponse {
  message: string;
}

export interface ResetPasswordRequest {
  userId: number;
  token: string;
  newPassword: string;
}

export interface ResetPasswordResponse {
  message: string;
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
  repostCount: number;
  replyCount: number;
}

export interface PostFeedResponse {
  items: PostFeedItemDto[];
  author: PostAuthorDto;
}

export interface PostResponse {
  id: number;
  text?: string;
  imageUrl?: string;
  createdAt: string;
  updatedAt?: string;
  author: PostAuthorDto;
  replyId?: number | null;
  repostCount: number;
  replyCount: number;
}

export interface ReplyFeedItemDto {
  id: number;
  text?: string;
  imageUrl?: string;
  createdAt: string;
  updatedAt?: string;
  parentPost?: PostResponse | null;
  repostCount: number;
  replyCount: number;
}

export interface ReplyFeedResponse {
  items: ReplyFeedItemDto[];
  author: PostAuthorDto;
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


export interface UploadImageResponse {
  url: string;
}


export interface ResendConfirmationEmailRequest {
  email: string;
}

export interface ResendConfirmationEmailResponse {
  message: string;
}


export interface SignOutResponse {
  message: string;
}
