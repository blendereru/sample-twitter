namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// A post's details returned upon successful retrieval.
/// </summary>
public record PostResponse(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorDto Author,
    long? ReplyId,
    int RepostCount,
    int ReplyCount);