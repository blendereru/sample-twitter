using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class FollowTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_SetsFollowerIdFollowingIdAndCreatedAt()
    {
        // Act
        var follow = Follow.Create(followerId: 10, followingId: 20, now: FixedNow);

        // Assert
        Assert.Equal(10, follow.FollowerId);
        Assert.Equal(20, follow.FollowingId);
        Assert.Equal(FixedNow, follow.CreatedAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Create_WhenFollowerIdOrFollowingIdIsNotPositive_ThrowsArgumentOutOfRangeException(
        long followerId,
        long followingId)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Follow.Create(followerId, followingId, FixedNow));
    }

    [Fact]
    public void Create_WhenFollowerIdEqualsFollowingId_ThrowsSelfFollowException()
    {
        // Act & Assert
        Assert.Throws<SelfFollowException>(() =>
            Follow.Create(followerId: 42, followingId: 42, now: FixedNow));
    }
}