namespace SampleTwitter.API.DTOs.ResponseDTOs;

/// <summary>
/// Embedded summary of a quoted post.
/// </summary>
public record QuotedPostDto(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorDto Author);