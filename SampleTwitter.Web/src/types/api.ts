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
