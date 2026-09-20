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

    [Fact]
    public async Task Repost_NonExistentPost_ThrowsPostNotFoundException()
    {
        // Arrange
        await SeedUser(userId: 1, email: "user1@example.com");

        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Repost(postId: 9999, userId: 1));
    }

    [Fact]
    public async Task Repost_NonExistentUser_ThrowsUserNotFoundException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _sut.Repost(postId: post.Id, userId: 9999));
    }

    [Fact]
    public async Task Repost_ValidPostAndUser_ReturnsRepostResultWithCorrectProperties()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");
        await SeedUser(userId: 2, email: "user2@example.com");
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = await _sut.Repost(postId: post.Id, userId: 2);

        // Assert
        Assert.Equal(post.Id, result.PostId);
        Assert.Equal(2, result.UserId);
        Assert.True(result.CreatedAt >= before);
    }

    [Fact]
    public async Task Repost_AlreadyReposted_ThrowsAlreadyRepostedException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");
        await SeedUser(userId: 2, email: "user2@example.com");

        await _sut.Repost(postId: post.Id, userId: 2);

        // Act & Assert
        await Assert.ThrowsAsync<AlreadyRepostedException>(
            () => _sut.Repost(postId: post.Id, userId: 2));
    }

    [Fact]
    public async Task Repost_SoftDeletedPost_ThrowsPostNotFoundException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");
        await SeedUser(userId: 2, email: "user2@example.com");
        await _sut.Delete(postId: post.Id, userId: 1);

        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Repost(postId: post.Id, userId: 2));
    }

    [Fact]
    public async Task UndoRepost_ExistingRepost_CompletesSuccessfully()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");
        await SeedRepost(postId: post.Id, userId: 2);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _sut.UndoRepost(postId: post.Id, userId: 2));
        Assert.Null(exception);

        await Assert.ThrowsAsync<RepostNotFoundException>(
            () => _sut.UndoRepost(postId: post.Id, userId: 2));
    }

    [Fact]
    public async Task UndoRepost_RepostNotFound_ThrowsRepostNotFoundException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "a post");

        // Act & Assert
        await Assert.ThrowsAsync<RepostNotFoundException>(
            () => _sut.UndoRepost(postId: post.Id, userId: 2));
    }

    [Fact]
    public async Task UndoRepost_PostNotFound_ThrowsRepostNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<RepostNotFoundException>(
            () => _sut.UndoRepost(postId: 99999, userId: 2));
    }

    [Fact]
    public async Task GetPosts_UserNotFound_ThrowsUserNotFoundException()
    {
        // Act & Asssert
        await Assert.ThrowsAsync<UserNotFoundException>(() => _sut.GetPosts(userId: 99999));
    }

    [Fact]
    public async Task GetPosts_UserHasNoPosts_ReturnsEmptyList()
    {
        // Arrange
        var user = await SeedUser(userId: 1, "user@email.com");

        // Act
        var posts = await _sut.GetPosts(userId: user.Id);

        // Assert
        Assert.Empty(posts.Items);
    }

    [Fact]
    public async Task GetPosts_ValidPosts_ReturnsPostsInDescendingChronologicalOrder()
    {
        // Arrange
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        var post1 = await SeedPost(userId: 1, text: "older post", createdAt: t1);
        var post2 = await SeedPost(userId: 1, text: "newer post", createdAt: t2);

        // Act
        var result = await _sut.GetPosts(userId: 1);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(post2.Id, result.Items[0].Id);
        Assert.Equal(post1.Id, result.Items[1].Id);
    }

    [Fact]
    public async Task GetPosts_SameCreatedAt_OrdersByDescendingId()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var post1 = await SeedPost(userId: 1, text: "post 1", createdAt: timestamp);
        var post2 = await SeedPost(userId: 1, text: "post 2", createdAt: timestamp);

        // Act
        var result = await _sut.GetPosts(userId: 1);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(post2.Id, result.Items[0].Id);
        Assert.Equal(post1.Id, result.Items[1].Id);
    }

    [Fact]
    public async Task GetPost_NonExistentPost_ThrowsPostNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(() => _sut.GetById(id: 99999));
    }

    private async Task<Repost> SeedRepost(long postId, long userId, DateTimeOffset? createdAt = null)
    {
        await SeedUser(userId, $"user{userId}@example.com");

        var repost = new Repost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        _applicationContext.Reposts.Add(repost);
        await _applicationContext.SaveChangesAsync();
        return repost;
    }

    private async Task<Post> SeedPost(
        long userId,
        string? text = null,
        string? imageUrl = null,
        long? replyId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        await SeedUser(userId, $"user{userId}@example.com");

        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt
        };
        _applicationContext.Posts.Add(post);
        await _applicationContext.SaveChangesAsync();
        return post;
    }

    private async Task<User> SeedUser(long userId, string email)
    {
        var existing = _applicationContext.Users.Local.FirstOrDefault(u => u.Id == userId);
        if (existing is not null)
        {
            return existing;
        }

        var user = UserCreationHelpers.CreateUser(userId, email);
        _applicationContext.Users.Add(user);
        await _applicationContext.SaveChangesAsync();
        return user;
    }

    public void Dispose() => _applicationContext.Dispose();
}