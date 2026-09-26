namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents a single reply in a feed.
/// </summary>
/// <param name="Id">The unique identifier of the reply.</param>
/// <param name="Text">The text content of the reply.</param>
/// <param name="ImageUrl">Optional image attachment URL.</param>
/// <param name="CreatedAt">The date and time when the reply was originally created.</param>
/// <param name="UpdatedAt">The date and time when the reply was last edited, or null if never edited.</param>
/// <param name="ParentPost">The post to which the reply was made to.</param>
/// <param name="RepostCount">The total number of reposts this reply has received.</param>
/// <param name="ReplyCount">The total number of replies this reply has received.</param>
public record ReplyFeedItemDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostFeedItemDto ParentPost,
    int RepostCount,
    int ReplyCount);