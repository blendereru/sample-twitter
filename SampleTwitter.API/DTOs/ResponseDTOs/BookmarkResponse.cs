namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents the result of bookmarking a post.
/// </summary>
/// <param name="PostId">The ID of the bookmarked post.</param>
/// <param name="UserId">The ID of the user who bookmarked the post.</param>
/// <param name="CreatedAt">The timestamp when the bookmark occurred.</param>
public record BookmarkResponse(
    long PostId,
    long UserId,
    DateTimeOffset CreatedAt);
