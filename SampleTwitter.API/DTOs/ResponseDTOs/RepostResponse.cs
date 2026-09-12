namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents the result of reposting a post.
/// </summary>
/// <param name="PostId">The ID of the reposted post.</param>
/// <param name="UserId">The ID of the user who reposted.</param>
/// <param name="CreatedAt">The timestamp when the repost occurred.</param>
public record RepostResponse(
    long PostId,
    long UserId,
    DateTimeOffset CreatedAt);