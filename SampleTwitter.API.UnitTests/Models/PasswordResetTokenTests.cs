using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class PasswordResetTokenTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_Sets1HourExpirationAndNullUsedAt()
    {
        // Act
        var token = PasswordResetToken.Create(userId: 7, tokenHash: "hashed-token", now: FixedNow);

        // Assert
        Assert.Equal(7, token.UserId);
        Assert.Equal("hashed-token", token.TokenHash);
        Assert.Equal(FixedNow, token.CreatedAt);
        Assert.Equal(FixedNow.AddHours(1), token.ExpiresAt);
        Assert.Null(token.UsedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WhenUserIdIsNotPositive_ThrowsArgumentOutOfRangeException(long invalidUserId)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PasswordResetToken.Create(invalidUserId, "hashed-token", FixedNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenTokenHashIsBlank_ThrowsArgumentException(string? invalidHash)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            PasswordResetToken.Create(1, invalidHash!, FixedNow));
    }

    [Fact]
    public void IsValid_WhenUnusedAndNotExpired_ReturnsTrueIncludingExactBoundary()
    {
        // Arrange
        var token = PasswordResetToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.True(token.IsValid(FixedNow.AddMinutes(30)));
        Assert.True(token.IsValid(FixedNow.Add(PasswordResetToken.DefaultLifetime)));
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var token = PasswordResetToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.False(token.IsValid(FixedNow.Add(PasswordResetToken.DefaultLifetime).AddTicks(1)));
    }

    [Fact]
    public void MarkUsed_WhenValid_SetsUsedAtAndMarksTokenInvalid()
    {
        // Arrange
        var token = PasswordResetToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);
        var usedTime = FixedNow.AddMinutes(15);

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
        var token = PasswordResetToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);
        var firstUsedTime = FixedNow.AddMinutes(10);
        token.MarkUsed(firstUsedTime);

        // Act & Assert
        Assert.Throws<InvalidTokenException>(() =>
            token.MarkUsed(FixedNow.AddMinutes(20)));

        Assert.Equal(firstUsedTime, token.UsedAt);
    }

    [Fact]
    public void MarkUsed_WhenExpired_ThrowsInvalidTokenExceptionAndLeavesUsedAtNull()
    {
        // Arrange
        var token = PasswordResetToken.Create(userId: 1, tokenHash: "hash", now: FixedNow);

        // Act & Assert
        Assert.Throws<InvalidTokenException>(() =>
            token.MarkUsed(FixedNow.AddHours(2)));

        Assert.Null(token.UsedAt);
    }
}