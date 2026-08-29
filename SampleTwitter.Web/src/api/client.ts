import axios, { AxiosError } from 'axios';
import type { ApiError, ProblemDetails } from '@/types/api';

export const apiClient = axios.create({
  baseURL: '/api',
  withCredentials: true,
  headers: {
    'Accept': 'application/json',
    'Content-Type': 'application/json',
  },
});

export function parseApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ProblemDetails>;
    const data = axiosError.response?.data;
    const status = axiosError.response?.status;

    if (data) {
      return {
        message: data.detail || data.title || axiosError.message,
        status: data.status || status,
        title: data.title,
        detail: data.detail,
        fieldErrors: data.errors,
      };
    }

    return {
      message: axiosError.message || 'An unexpected network error occurred.',
      status,
    };
  }

  if (error instanceof Error) {
    return {
      message: error.message,
    };
  }

  return {
    message: 'An unknown error occurred.',
  };
}
