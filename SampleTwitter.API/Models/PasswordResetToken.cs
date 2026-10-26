using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class PasswordResetToken
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    public long Id { get; private set; }
    public long UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Create"/>.
    /// </summary>
    private PasswordResetToken()
    {
    }

    public static PasswordResetToken Create(
        long userId,
        string tokenHash,
        DateTimeOffset? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var createdAt = now ?? DateTimeOffset.UtcNow;

        return new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(DefaultLifetime)
        };
    }

    public bool IsValid(DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;
        return UsedAt is null && ExpiresAt >= currentTime;
    }

    public void MarkUsed(DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;
        if (!IsValid(currentTime))
        {
            throw new InvalidTokenException("This password reset link is invalid or has expired.");
        }

        UsedAt = currentTime;
    }
}