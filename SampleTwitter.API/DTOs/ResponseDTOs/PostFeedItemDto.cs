namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents a single post in a feed.
/// </summary>
/// <param name="Id">The unique identifier of the post.</param>
/// <param name="Text">The text content of the post.</param>
/// <param name="ImageUrl">Optional image attachment URL.</param>
/// <param name="CreatedAt">The date and time when the post was originally created.</param>
/// <param name="UpdatedAt">The date and time when the post was last edited, or null if never edited.</param>
/// <param name="RepostCount">The total number of reposts this post has received.</param>
/// <param name="ReplyCount">The total number of direct replies this post has received.</param>
public record PostFeedItemDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int RepostCount,
    int ReplyCount
);