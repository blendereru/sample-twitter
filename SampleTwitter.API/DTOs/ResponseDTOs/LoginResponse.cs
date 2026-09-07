namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Authenticated account details returned upon successful sign-in.
/// </summary>
public record LoginResponse(long UserId, string Email, string Message);
