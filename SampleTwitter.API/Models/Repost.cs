namespace SampleTwitter.API.Models;

public class Repost
{
    public long PostId { get; private set; }
    public Post Post { get; private set; } = null!;
    public long UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Create"/>.
    /// </summary>
    private Repost()
    {
    }

    public static Repost Create(long postId, long userId, DateTimeOffset? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(postId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        return new Repost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }
}