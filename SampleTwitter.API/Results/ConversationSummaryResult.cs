namespace SampleTwitter.API.Results;

public record ConversationSummaryResult(
    long Id,
    ConversationParticipantResult OtherParticipant,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    MessageResult? LastMessage,
    int UnreadCount,
    DateTimeOffset? OtherParticipantLastReadAt);