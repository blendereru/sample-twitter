namespace SampleTwitter.API.Results;

public record EditProfileResult(
    long Id,
    string? Username,
    string? ImageUrl,
    DateTimeOffset? UpdatedAt);