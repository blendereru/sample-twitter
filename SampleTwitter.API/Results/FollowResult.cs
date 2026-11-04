namespace SampleTwitter.API.Results;

public record FollowResult(long FollowerId, long FollowingId, DateTimeOffset CreatedAt);