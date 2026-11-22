namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record ConversationResponse(
    long Id,
    ConversationParticipantDto OtherParticipant,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastMessageAt,
    MessageResponse? LastMessage,
    int UnreadCount,
    DateTimeOffset? OtherParticipantLastReadAt);