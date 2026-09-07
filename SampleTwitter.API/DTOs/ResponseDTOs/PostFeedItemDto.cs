namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents a single post in the profile feed.
/// </summary>
/// <param name="ParentPost">Parent post context if this post is a continuation of a self-thread.</param>
public record PostFeedItemDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorDto Author,
    PostFeedItemDto? ParentPost
);