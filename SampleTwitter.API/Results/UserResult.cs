namespace SampleTwitter.API.Results;

public record UserResult(
    long Id,
    string Email,
    DateTimeOffset RegisteredAt,
    string? Username = null,
    string? ImageUrl = null,
    DateTimeOffset? UpdatedAt = null);