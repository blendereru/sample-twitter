import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import type { ConversationReadDto, MessageResponse, TypingDto } from '@/types/api';

type MessageHandler = (message: MessageResponse) => void;
type ReadHandler = (receipt: ConversationReadDto) => void;
type TypingHandler = (typing: TypingDto) => void;
type ReconnectedHandler = () => void;

class ChatConnectionManager {
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  private messageHandlers = new Set<MessageHandler>();
  private readHandlers = new Set<ReadHandler>();
  private typingHandlers = new Set<TypingHandler>();
  private reconnectedHandlers = new Set<ReconnectedHandler>();

  private ensureConnection(): HubConnection {
    if (this.connection) {
      return this.connection;
    }

    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on('MessageReceived', (message: MessageResponse) => {
      for (const handler of this.messageHandlers) {
        handler(message);
      }
    });

    conn.on('ConversationRead', (receipt: ConversationReadDto) => {
      for (const handler of this.readHandlers) {
        handler(receipt);
      }
    });

    conn.on('TypingStarted', (typing: TypingDto) => {
      for (const handler of this.typingHandlers) {
        handler(typing);
      }
    });

    conn.onreconnected(() => {
      for (const handler of this.reconnectedHandlers) {
        handler();
      }
    });

    this.connection = conn;
    return conn;
  }

  async start(): Promise<void> {
    const conn = this.ensureConnection();
    if (conn.state === HubConnectionState.Connected) {
      return;
    }
    if (this.startPromise) {
      return this.startPromise;
    }

    this.startPromise = conn
      .start()
      .catch((err) => {
        console.warn('SignalR connection failed to start:', err);
      })
      .finally(() => {
        this.startPromise = null;
      });

    return this.startPromise;
  }

  async stop(): Promise<void> {
    if (!this.connection) {
      return;
    }
    const conn = this.connection;
    this.connection = null;
    this.startPromise = null;
    if (conn.state !== HubConnectionState.Disconnected) {
      try {
        await conn.stop();
      } catch {
        // Ignore stop errors during logout/cleanup.
      }
    }
  }

  async sendTyping(conversationId: number): Promise<void> {
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }
    try {
      await this.connection.invoke('Typing', conversationId);
    } catch {
      // Transient typing notifications are best-effort.
    }
  }

  onMessageReceived(handler: MessageHandler): () => void {
    this.messageHandlers.add(handler);
    return () => this.messageHandlers.delete(handler);
  }

  onConversationRead(handler: ReadHandler): () => void {
    this.readHandlers.add(handler);
    return () => this.readHandlers.delete(handler);
  }

  onTypingStarted(handler: TypingHandler): () => void {
    this.typingHandlers.add(handler);
    return () => this.typingHandlers.delete(handler);
  }

  onReconnected(handler: ReconnectedHandler): () => void {
    this.reconnectedHandlers.add(handler);
    return () => this.reconnectedHandlers.delete(handler);
  }
}

export const chatConnection = new ChatConnectionManager();
