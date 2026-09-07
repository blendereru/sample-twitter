namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Profile details of the currently authenticated user.
/// </summary>
public record MeResponse(long Id, string Email, DateTimeOffset RegisteredAt);
