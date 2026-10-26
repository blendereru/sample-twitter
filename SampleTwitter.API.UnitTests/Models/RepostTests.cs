using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class RepostTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_SetsPostIdUserIdAndCreatedAt()
    {
        // Act
        var repost = Repost.Create(postId: 100, userId: 42, now: FixedNow);

        // Assert
        Assert.Equal(100, repost.PostId);
        Assert.Equal(42, repost.UserId);
        Assert.Equal(FixedNow, repost.CreatedAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Create_WhenPostIdOrUserIdIsNotPositive_ThrowsArgumentOutOfRangeException(
        long postId,
        long userId)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Repost.Create(postId, userId, FixedNow));
    }
}