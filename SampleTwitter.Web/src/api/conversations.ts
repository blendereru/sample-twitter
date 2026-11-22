import { apiClient } from '@/api/client';
import type {
  ConversationResponse,
  MessageResponse,
  StartConversationRequest,
  SendMessageRequest,
} from '@/types/api';

export async function startConversation(recipientId: number): Promise<ConversationResponse> {
  const payload: StartConversationRequest = { recipientId };
  const { data } = await apiClient.post<ConversationResponse>('/conversations', payload);
  return data;
}

export async function getConversations(): Promise<ConversationResponse[]> {
  const { data } = await apiClient.get<ConversationResponse[]>('/conversations');
  return data;
}

export async function getMessages(
  conversationId: number,
  params?: { before?: number; after?: number; limit?: number }
): Promise<MessageResponse[]> {
  const { data } = await apiClient.get<MessageResponse[]>(`/conversations/${conversationId}/messages`, {
    params,
  });
  return data;
}

export async function sendMessage(conversationId: number, text: string): Promise<MessageResponse> {
  const payload: SendMessageRequest = { text };
  const { data } = await apiClient.post<MessageResponse>(`/conversations/${conversationId}/messages`, payload);
  return data;
}

export async function markConversationAsRead(conversationId: number): Promise<void> {
  await apiClient.post(`/conversations/${conversationId}/read`);
}
