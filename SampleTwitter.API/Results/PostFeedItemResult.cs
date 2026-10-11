namespace SampleTwitter.API.Results;

public record PostFeedItemResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int RepostCount,
    int ReplyCount);