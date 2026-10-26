using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class PostTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithTextOnly_InitializesPostWithNormalizedText()
    {
        // Act
        var post = Post.Create(
            userId: 42,
            text: "  hello world  ",
            imageUrl: null,
            replyId: null,
            now: FixedNow);

        // Assert
        Assert.Equal(42, post.UserId);
        Assert.Equal("hello world", post.Text);
        Assert.Null(post.ImageUrl);
        Assert.Null(post.ReplyId);
        Assert.Equal(FixedNow, post.CreatedAt);
        Assert.Null(post.UpdatedAt);
        Assert.False(post.IsDeleted);
        Assert.Null(post.DeletedAt);
    }

    [Fact]
    public void Create_WithImageOnly_InitializesPostWithNormalizedImageUrl()
    {
        // Act
        var post = Post.Create(
            userId: 42,
            text: null,
            imageUrl: "  https://example.com/photo.png  ",
            replyId: null,
            now: FixedNow);

        // Assert
        Assert.Equal(42, post.UserId);
        Assert.Null(post.Text);
        Assert.Equal("https://example.com/photo.png", post.ImageUrl);
        Assert.Equal(FixedNow, post.CreatedAt);
    }

    [Fact]
    public void Create_WithTextAndImageAndReplyId_SetsAllProperties()
    {
        // Act
        var post = Post.Create(
            userId: 42,
            text: "reply text",
            imageUrl: "https://example.com/photo.png",
            replyId: 99,
            now: FixedNow);

        // Assert
        Assert.Equal(42, post.UserId);
        Assert.Equal("reply text", post.Text);
        Assert.Equal("https://example.com/photo.png", post.ImageUrl);
        Assert.Equal(99, post.ReplyId);
        Assert.Equal(FixedNow, post.CreatedAt);
    }

    [Fact]
    public void Create_WithWhitespaceTextAndValidImage_NormalizesTextToNull()
    {
        // Act
        var post = Post.Create(
            userId: 42,
            text: "   \t\n  ",
            imageUrl: "https://example.com/photo.png",
            replyId: null,
            now: FixedNow);

        // Assert
        Assert.Null(post.Text);
        Assert.Equal("https://example.com/photo.png", post.ImageUrl);
    }

    [Fact]
    public void Create_WithValidTextAndWhitespaceImage_NormalizesImageUrlToNull()
    {
        // Act
        var post = Post.Create(
            userId: 42,
            text: "valid text",
            imageUrl: "   ",
            replyId: null,
            now: FixedNow);

        // Assert
        Assert.Equal("valid text", post.Text);
        Assert.Null(post.ImageUrl);
    }

    [Fact]
    public void Create_WithExactlyMaxTextLength_Succeeds()
    {
        // Arrange
        var maxText = new string('a', Post.MaxTextLength);

        // Act
        var post = Post.Create(
            userId: 1,
            text: $"  {maxText}  ",
            imageUrl: null,
            replyId: null,
            now: FixedNow);

        // Assert
        Assert.Equal(maxText, post.Text);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    [InlineData(" \t ", " \n ")]
    public void Create_WhenBothTextAndImageAreBlank_ThrowsEmptyPostException(
        string? text,
        string? imageUrl)
    {
        // Act & Assert
        Assert.Throws<EmptyPostException>(() =>
            Post.Create(userId: 1, text: text, imageUrl: imageUrl, replyId: null, now: FixedNow));
    }

    [Fact]
    public void Create_WhenTrimmedTextExceedsMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var tooLong = new string('x', Post.MaxTextLength + 1);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            Post.Create(userId: 1, text: tooLong, imageUrl: null, replyId: null, now: FixedNow));

        Assert.Equal("text", ex.ParamName);
    }

    [Fact]
    public void Edit_ByOwner_UpdatesContentAndTimestampAndPreservesIdentityFields()
    {
        // Arrange
        var post = Post.Create(
            userId: 10,
            text: "original",
            imageUrl: "https://example.com/old.png",
            replyId: 55,
            now: FixedNow);
        var editTime = FixedNow.AddMinutes(15);

        // Act
        post.Edit(
            editorUserId: 10,
            text: "  updated text  ",
            imageUrl: "  https://example.com/new.png  ",
            now: editTime);

        // Assert
        Assert.Equal("updated text", post.Text);
        Assert.Equal("https://example.com/new.png", post.ImageUrl);
        Assert.Equal(editTime, post.UpdatedAt);
        Assert.Equal(10, post.UserId);
        Assert.Equal(55, post.ReplyId);
        Assert.Equal(FixedNow, post.CreatedAt);
    }

    [Fact]
    public void Edit_RemovingImageWhileKeepingText_SetsImageUrlToNull()
    {
        // Arrange
        var post = Post.Create(
            userId: 10,
            text: "keep text",
            imageUrl: "https://example.com/old.png",
            replyId: null,
            now: FixedNow);

        // Act
        post.Edit(editorUserId: 10, text: "keep text", imageUrl: "   ", now: FixedNow.AddMinutes(5));

        // Assert
        Assert.Equal("keep text", post.Text);
        Assert.Null(post.ImageUrl);
    }

    [Fact]
    public void Edit_RemovingTextWhileKeepingImage_SetsTextToNull()
    {
        // Arrange
        var post = Post.Create(
            userId: 10,
            text: "old text",
            imageUrl: null,
            replyId: null,
            now: FixedNow);

        // Act
        post.Edit(
            editorUserId: 10,
            text: "   ",
            imageUrl: "https://example.com/photo.png",
            now: FixedNow.AddMinutes(5));

        // Assert
        Assert.Null(post.Text);
        Assert.Equal("https://example.com/photo.png", post.ImageUrl);
    }

    [Fact]
    public void Edit_WithExactlyMaxTextLength_Succeeds()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "initial", imageUrl: null, replyId: null, now: FixedNow);
        var maxText = new string('b', Post.MaxTextLength);

        // Act
        post.Edit(editorUserId: 10, text: maxText, imageUrl: null, now: FixedNow.AddMinutes(1));

        // Assert
        Assert.Equal(maxText, post.Text);
    }

    [Fact]
    public void Edit_ByDifferentUser_ThrowsForbiddenExceptionAndDoesNotMutateState()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "original", imageUrl: null, replyId: null, now: FixedNow);

        // Act & Assert
        Assert.Throws<ForbiddenException>(() =>
            post.Edit(editorUserId: 99, text: "hacked", imageUrl: null, now: FixedNow.AddMinutes(1)));

        Assert.Equal("original", post.Text);
        Assert.Null(post.UpdatedAt);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", null)]
    [InlineData(null, "   ")]
    public void Edit_WhenBothTextAndImageAreBlank_ThrowsEmptyPostExceptionAndDoesNotMutateState(
        string? text,
        string? imageUrl)
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "original", imageUrl: null, replyId: null, now: FixedNow);

        // Act & Assert
        Assert.Throws<EmptyPostException>(() =>
            post.Edit(editorUserId: 10, text: text, imageUrl: imageUrl, now: FixedNow.AddMinutes(1)));

        Assert.Equal("original", post.Text);
        Assert.Null(post.UpdatedAt);
    }

    [Fact]
    public void Edit_WhenTextExceedsMaxLength_ThrowsArgumentExceptionAndDoesNotMutateState()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "original", imageUrl: null, replyId: null, now: FixedNow);
        var tooLong = new string('x', Post.MaxTextLength + 1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            post.Edit(editorUserId: 10, text: tooLong, imageUrl: null, now: FixedNow.AddMinutes(1)));

        Assert.Equal("original", post.Text);
        Assert.Null(post.UpdatedAt);
    }

    [Fact]
    public void Delete_ByOwner_MarksPostSoftDeletedWithTimestamp()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "to delete", imageUrl: null, replyId: null, now: FixedNow);
        var deleteTime = FixedNow.AddHours(1);

        // Act
        post.Delete(actorUserId: 10, now: deleteTime);

        // Assert
        Assert.True(post.IsDeleted);
        Assert.Equal(deleteTime, post.DeletedAt);
        Assert.Equal("to delete", post.Text);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_IsIdempotentAndPreservesOriginalDeletedAt()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "to delete", imageUrl: null, replyId: null, now: FixedNow);
        var firstDeleteTime = FixedNow.AddHours(1);
        post.Delete(actorUserId: 10, now: firstDeleteTime);

        // Act
        post.Delete(actorUserId: 10, now: firstDeleteTime.AddHours(2));

        // Assert
        Assert.True(post.IsDeleted);
        Assert.Equal(firstDeleteTime, post.DeletedAt);
    }

    [Fact]
    public void Delete_ByDifferentUser_ThrowsForbiddenExceptionAndDoesNotDeletePost()
    {
        // Arrange
        var post = Post.Create(userId: 10, text: "protected", imageUrl: null, replyId: null, now: FixedNow);

        // Act & Assert
        Assert.Throws<ForbiddenException>(() =>
            post.Delete(actorUserId: 99, now: FixedNow.AddHours(1)));

        Assert.False(post.IsDeleted);
        Assert.Null(post.DeletedAt);
    }
}