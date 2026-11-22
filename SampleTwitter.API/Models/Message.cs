namespace SampleTwitter.API.Models;

public class Message
{
    public const int MaxTextLength = 1000;

    public long Id { get; private set; }
    public long ConversationId { get; private set; }
    public Conversation Conversation { get; private set; } = null!;
    public long SenderId { get; private set; }
    public User Sender { get; private set; } = null!;
    public string Text { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Conversation.SendMessage"/>.
    /// </summary>
    private Message()
    {
    }

    internal static Message Create(long conversationId, long senderId, string text, DateTimeOffset sentAt)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Text = text,
            SentAt = sentAt
        };
    }

    internal void MarkRead(DateTimeOffset readAt)
    {
        ReadAt ??= readAt;
    }
}