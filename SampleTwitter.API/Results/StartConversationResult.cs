namespace SampleTwitter.API.Results;

public record StartConversationResult(
    long Id,
    ConversationParticipantResult OtherParticipant,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    bool IsNew);