using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class BookmarkTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidArguments_SetsPostIdUserIdAndCreatedAt()
    {
        // Act
        var bookmark = Bookmark.Create(postId: 100, userId: 42, now: FixedNow);

        // Assert
        Assert.Equal(100, bookmark.PostId);
        Assert.Equal(42, bookmark.UserId);
        Assert.Equal(FixedNow, bookmark.CreatedAt);
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
            Bookmark.Create(postId, userId, FixedNow));
    }
}