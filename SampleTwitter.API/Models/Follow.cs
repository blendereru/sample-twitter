using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class Follow
{
    public long FollowerId { get; private set; }
    public User Follower { get; private set; } = null!;
    public long FollowingId { get; private set; }
    public User Following { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Create"/>.
    /// </summary>
    private Follow()
    {
    }

    public static Follow Create(long followerId, long followingId, DateTimeOffset? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(followingId);

        if (followerId == followingId)
        {
            throw new SelfFollowException($"User {followerId} attempted to follow themselves.");
        }

        return new Follow
        {
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }
}