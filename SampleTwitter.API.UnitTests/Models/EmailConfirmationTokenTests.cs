using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class EmailConfirmationTokenTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_Sets24HourExpirationAndNullUsedAt()
    {
        // Act
        var token = EmailConfirmationToken.Create(userId: 12, tokenHash: "confirm-hash", now: FixedNow);

        // Assert
        Assert.Equal(12, token.UserId);
        Assert.Equal("confirm-hash", token.TokenHash);
        Assert.Equal(FixedNow, token.CreatedAt);
        Assert.Equal(FixedNow.AddHours(24), token.ExpiresAt);
        Assert.Null(token.UsedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WhenUserIdIsNotPositive_ThrowsArgumentOutOfRangeException(long invalidUserId)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EmailConfirmationToken.Create(invalidUserId, "confirm-hash", FixedNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenTokenHashIsBlank_ThrowsArgumentException(string? invalidHash)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            EmailConfirmationToken.Create(1, invalidHash!, FixedNow));
    }

    [Fact]
    public void IsValid_WhenUnusedAndNotExpired_ReturnsTrueIncludingExactBoundary()
    {
        // Arrange
        var token = EmailConfirmationToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.True(token.IsValid(FixedNow.AddHours(12)));
        Assert.True(token.IsValid(FixedNow.Add(EmailConfirmationToken.DefaultLifetime)));
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var token = EmailConfirmationToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.False(token.IsValid(FixedNow.Add(EmailConfirmationToken.DefaultLifetime).AddTicks(1)));
    }

    [Fact]
    public void MarkUsed_WhenValid_SetsUsedAtAndMarksTokenInvalid()
    {
        // Arrange
        var token = EmailConfirmationToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);
        var usedTime = FixedNow.AddHours(2);

        // Act
        token.MarkUsed(usedTime);

        // Assert
        Assert.Equal(usedTime, token.UsedAt);
        Assert.False(token.IsValid(usedTime));
    }

    [Fact]
    public void MarkUsed_WhenAlreadyUsed_ThrowsInvalidTokenExceptionAndPreservesOriginalUsedAt()
    {
        // Arrange
        var token = EmailConfirmationToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);
        var firstUsedTime = FixedNow.AddHours(1);
        token.MarkUsed(firstUsedTime);

        // Act & Assert
        Assert.Throws<InvalidTokenException>(() =>
            token.MarkUsed(FixedNow.AddHours(2)));

        Assert.Equal(firstUsedTime, token.UsedAt);
    }

    [Fact]
    public void MarkUsed_WhenExpired_ThrowsInvalidTokenExceptionAndLeavesUsedAtNull()
    {
        // Arrange
        var token = EmailConfirmationToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.Throws<InvalidTokenException>(() =>
            token.MarkUsed(FixedNow.AddHours(25)));

        Assert.Null(token.UsedAt);
    }
}