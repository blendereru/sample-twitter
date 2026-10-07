namespace SampleTwitter.API.Results;

public record ReplyFeedResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorResult Author,
    int RepostCount,
    int ReplyCount);