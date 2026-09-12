namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents a single post in the profile feed.
/// </summary>
/// <param name="Id">The unique identifier of the post.</param>
/// <param name="Text">The text content of the post.</param>
/// <param name="ImageUrl">Optional image attachment URL.</param>
/// <param name="CreatedAt">The date and time when the post was originally created.</param>
/// <param name="UpdatedAt">The date and time when the post was last edited, or null if never edited.</param>
/// <param name="Author">The author of the post.</param>
/// <param name="ParentPost">Parent post context if this post is a continuation of a self-thread.</param>
/// <param name="IsRepost">Indicates whether this item appears in the feed as a repost.</param>
/// <param name="RepostedBy">The user who reposted this post, if this item is a repost.</param>
public record PostFeedItemDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorDto Author,
    PostFeedItemDto? ParentPost,
    bool IsRepost,
    PostAuthorDto? RepostedBy
);