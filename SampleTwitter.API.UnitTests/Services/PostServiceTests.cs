using Microsoft.Extensions.Logging.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Services;
using SampleTwitter.API.UnitTests.Helpers;

namespace SampleTwitter.API.UnitTests.Services;

public class PostServiceTests : IDisposable
{
    private readonly ApplicationContext _applicationContext;
    private readonly PostService _sut;

    public PostServiceTests()
    {
        _applicationContext = TestDbContextFactory.Create();
        _sut = new PostService(_applicationContext, NullLogger<PostService>.Instance);
    }

    [Fact]
    public async Task Create_NullTextAndNullImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new CreatePostRequest { Text = null, ImageUrl = null };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Create(request, userId: 1));
    }

    [Fact]
    public async Task Create_WhitespaceTextAndNullImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "   ", ImageUrl = null };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Create(request, userId: 1));
    }

    [Fact]
    public async Task Create_TextWithLeadingAndTrailingWhitespace_TrimsText()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "  trimmed text  " };

        // Act
        var result = await _sut.Create(request, userId: 1);

        // Assert
        Assert.Equal("trimmed text", result.Text);
    }

    [Fact]
    public async Task Create_NullTextAndWhitespaceImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new CreatePostRequest { Text = null, ImageUrl = "   " };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Create(request, userId: 1));
    }

    [Fact]
    public async Task Create_WhitespaceTextAndWhitespaceImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "   ", ImageUrl = "   " };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Create(request, userId: 1));
    }

    [Fact]
    public async Task Create_NonExistentReplyId_ThrowsPostNotFoundException()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "valid text", ReplyId = 9999 };

        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Create(request, userId: 1));
    }

    [Fact]
    public async Task Create_ExistingReplyId_SetsReplyIdCorrectly()
    {
        // Arrange
        var parent = await SeedPost(userId: 1, text: "parent post");
        var request = new CreatePostRequest { Text = "reply post", ReplyId = parent.Id };

        // Act
        var result = await _sut.Create(request, userId: 1);

        // Assert
        Assert.Equal(parent.Id, result.ReplyId);
    }

    [Fact]
    public async Task Create_TextOnly_SetsPropertiesCorrectly()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "text only" };
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.Create(request, userId: 42);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal("text only", result.Text);
        Assert.Null(result.ImageUrl);
        Assert.Null(result.ReplyId);
        Assert.Equal(42, result.UserId);
        Assert.True(result.CreatedAt >= before);
    }

    [Fact]
    public async Task Create_ImageOnly_SetsPropertiesCorrectly()
    {
        // Arrange
        var request = new CreatePostRequest { ImageUrl = "https://example.com/pic.png" };

        // Act
        var result = await _sut.Create(request, userId: 42);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Null(result.Text);
        Assert.Equal("https://example.com/pic.png", result.ImageUrl);
        Assert.Null(result.ReplyId);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public async Task Create_TextAndImage_SetsBoth()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Text = "has text",
            ImageUrl = "https://example.com/pic.png"
        };

        // Act
        var result = await _sut.Create(request, userId: 42);

        // Assert
        Assert.Equal("has text", result.Text);
        Assert.Equal("https://example.com/pic.png", result.ImageUrl);
        Assert.Equal(42, result.UserId);
    }

    [Fact]
    public async Task Edit_NullTextAndNullImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new EditPostRequest { Text = null, ImageUrl = null };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Edit(postId: 1, request, userId: 1));
    }

    [Fact]
    public async Task Edit_WhitespaceTextAndNullImage_ThrowsEmptyPostException()
    {
        // Arrange
        var request = new EditPostRequest { Text = "   ", ImageUrl = null };

        // Act & Assert
        await Assert.ThrowsAsync<EmptyPostException>(
            () => _sut.Edit(postId: 1, request, userId: 1));
    }

    [Fact]
    public async Task Edit_NonExistentPostId_ThrowsPostNotFoundException()
    {
        // Arrange
        var request = new EditPostRequest { Text = "valid text" };

        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Edit(postId: 9999, request, userId: 1));
    }

    [Fact]
    public async Task Edit_DifferentUser_ThrowsForbiddenException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "original");
        var request = new EditPostRequest { Text = "updated" };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.Edit(post.Id, request, userId: 2));
    }

    [Fact]
    public async Task Edit_TextWithWhitespace_TrimsText()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "original");
        var request = new EditPostRequest { Text = "  trimmed edit  " };

        // Act
        var result = await _sut.Edit(post.Id, request, userId: 1);

        // Assert
        Assert.Equal("trimmed edit", result.Text);
    }

    private async Task<Post> SeedPost(
        long userId,
        string? text = null,
        string? imageUrl = null,
        long? replyId = null)
    {
        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _applicationContext.Posts.Add(post);
        await _applicationContext.SaveChangesAsync();
        return post;
    }

    public void Dispose() => _applicationContext.Dispose();
}