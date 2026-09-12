namespace SampleTwitter.API.Results;

public record CreatePostResult(
    long Id,
    string? Text,
    string? ImageUrl,
    long? ReplyId,
    long UserId,
    DateTimeOffset CreatedAt);