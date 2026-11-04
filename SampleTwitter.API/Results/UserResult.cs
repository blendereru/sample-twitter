namespace SampleTwitter.API.Results;

public record UserResult(
    long Id,
    string Email,
    DateTimeOffset RegisteredAt,
    string? Username = null,
    string? ImageUrl = null,
    DateTimeOffset? UpdatedAt = null,
    int FollowersCount = 0,
    int FollowingCount = 0,
    bool IsFollowing = false);