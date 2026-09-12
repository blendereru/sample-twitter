namespace SampleTwitter.API.Results;

public record RepostResult(
    long PostId,
    long UserId,
    DateTimeOffset CreatedAt);