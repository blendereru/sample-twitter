namespace SampleTwitter.API.DTOs.ResponseDTOs;

public record MeResponse(long Id, string Email, DateTimeOffset RegisteredAt);
