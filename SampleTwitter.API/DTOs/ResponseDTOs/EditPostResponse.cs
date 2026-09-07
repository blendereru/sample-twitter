namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Response returned when a post is successfully edited.
/// </summary>
public record EditPostResponse(long PostId, string Message);