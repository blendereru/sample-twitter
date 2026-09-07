namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Response returned when a post is successfully created.
/// </summary>
public record CreatePostResponse(long PostId, string Message);