namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record MessageResponse(
    long Id,
    long ConversationId,
    long SenderId,
    string SenderUsername,
    string Text,
    DateTimeOffset SentAt);