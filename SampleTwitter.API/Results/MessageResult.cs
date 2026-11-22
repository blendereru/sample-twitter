namespace SampleTwitter.API.Results;

public record MessageResult(
    long Id,
    long ConversationId,
    long SenderId,
    string SenderUsername,
    string Text,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt = null);