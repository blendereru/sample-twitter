namespace SampleTwitter.API.Results;

public record QuotedPostResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorResult Author);