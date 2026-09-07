namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Response returned when account registration succeeds.
/// </summary>
public record SignUpResponse(long UserId, string Message);