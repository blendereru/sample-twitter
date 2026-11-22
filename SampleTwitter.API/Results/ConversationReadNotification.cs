namespace SampleTwitter.API.Results;

public record ConversationReadNotification(
    long ConversationId,
    long ReaderId,
    long RecipientId,
    DateTimeOffset ReadAt);