namespace SampleTwitter.API.Results;

public record EditPostResult(
    long Id,
    string? Text,
    string? ImageUrl,
    DateTimeOffset? UpdatedAt);