namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record ConversationReadDto(long ConversationId, long ReaderId, DateTimeOffset ReadAt);
