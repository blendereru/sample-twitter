namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Represents a single post card in the profile feed.
/// When the post is a self-reply (the user replied to their own post),
/// <see cref="ParentPost"/> is populated so the client can render the thread context above it.
/// </summary>
public record PostFeedItemDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorDto Author,
    PostFeedItemDto? ParentPost
);