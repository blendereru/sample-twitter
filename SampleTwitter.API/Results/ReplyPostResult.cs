namespace SampleTwitter.API.Results;

public record ReplyPostResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    PostResult ParentPost,
    int RepostCount,
    int ReplyCount);