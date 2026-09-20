namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Profile details of the user.
/// </summary>
public record UserResponse(long Id, string Email, DateTimeOffset RegisteredAt);