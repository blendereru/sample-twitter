namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Response returned when a post is successfully edited, containing the updated post representation.
/// </summary>
public record EditPostResponse(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset? UpdatedAt);