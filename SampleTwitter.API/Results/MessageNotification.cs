namespace SampleTwitter.API.Results;

public record MessageNotification(
    long Id,
    long ConversationId,
    long SenderId,
    string SenderUsername,
    long RecipientId,
    string Text,
    DateTimeOffset SentAt);