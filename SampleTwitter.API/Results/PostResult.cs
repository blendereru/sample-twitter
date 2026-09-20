namespace SampleTwitter.API.Results;

public record PostResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorResult Author,
    long? ReplyId,
    int RepostCount,
    int ReplyCount);