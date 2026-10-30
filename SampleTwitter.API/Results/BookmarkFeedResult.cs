namespace SampleTwitter.API.Results;

public record BookmarkFeedResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostAuthorResult Author,
    long? ReplyId,
    int RepostCount,
    int ReplyCount,
    bool IsReposted = false,
    PostAuthorResult? ReplyToAuthor = null);