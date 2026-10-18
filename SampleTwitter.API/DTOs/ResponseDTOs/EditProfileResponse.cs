namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Response returned when a user's profile is successfully edited, containing the updated profile representation.
/// </summary>
public record EditProfileResponse(
    long Id,
    string? Username,
    string? ImageUrl,
    DateTimeOffset? UpdatedAt);
