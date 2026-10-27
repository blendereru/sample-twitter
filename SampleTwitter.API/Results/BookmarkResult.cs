namespace SampleTwitter.API.Results;

public record BookmarkResult(
    long PostId,
    long UserId,
    DateTimeOffset CreatedAt);