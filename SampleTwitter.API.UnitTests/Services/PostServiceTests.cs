using Microsoft.EntityFrameworkCore;
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
    public async Task GetProfileFeed_UserDoesNotExist_ThrowsUserNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _sut.GetProfileFeed(userId: 9999));
    }

    [Fact]
    public async Task GetProfileFeed_UserExistsWithNoPosts_ReturnsEmptyList()
    {
        // Arrange
        await SeedUser(userId: 1, "user@example.com");

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProfileFeed_UserHasTopLevelPosts_ReturnsPostsInDescendingChronologicalOrder()
    {
        // Arrange
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        var post1 = await SeedPost(userId: 1, text: "older post", createdAt: t1);
        var post2 = await SeedPost(userId: 1, text: "newer post", createdAt: t2);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(post2.Id, result.Items[0].Id);
        Assert.Equal(post1.Id, result.Items[1].Id);
    }

    [Fact]
    public async Task GetProfileFeed_SameCreatedAt_OrdersByDescendingId()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var post1 = await SeedPost(userId: 1, text: "post 1", createdAt: timestamp);
        var post2 = await SeedPost(userId: 1, text: "post 2", createdAt: timestamp);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(post2.Id, result.Items[0].Id);
        Assert.Equal(post1.Id, result.Items[1].Id);
    }

    [Fact]
    public async Task GetProfileFeed_SelfThreadReplies_IncludesSelfReplyAndMapsParentPost()
    {
        // Arrange
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var parentPost = await SeedPost(userId: 1, text: "thread root", createdAt: t1);
        var replyPost = await SeedPost(userId: 1, text: "thread continuation", replyId: parentPost.Id, createdAt: t2);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.Equal(2, result.Items.Count);

        var replyItem = result.Items[0];
        Assert.Equal(replyPost.Id, replyItem.Id);
        Assert.NotNull(replyItem.ParentPost);
        Assert.Equal(parentPost.Id, replyItem.ParentPost.Id);
        Assert.Equal(parentPost.Text, replyItem.ParentPost.Text);
        Assert.Equal(1, replyItem.ParentPost.Author.Id);

        var parentItem = result.Items[1];
        Assert.Equal(parentPost.Id, parentItem.Id);
        Assert.Null(parentItem.ParentPost);
    }

    [Fact]
    public async Task GetProfileFeed_ReplyToAnotherUser_ExcludesReplyFromProfileFeed()
    {
        // Arrange
        var otherUserPost = await SeedPost(userId: 2, text: "other user's post");
        var conversationalReply = await SeedPost(userId: 1, text: "replying to user 2", replyId: otherUserPost.Id);
        var topLevelPost = await SeedPost(userId: 1, text: "top level post");

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(topLevelPost.Id, item.Id);
        Assert.DoesNotContain(result.Items, p => p.Id == conversationalReply.Id);
    }

    [Fact]
    public async Task GetProfileFeed_OtherUsersPosts_ExcludesOtherUsersPosts()
    {
        // Arrange
        var postUser1 = await SeedPost(userId: 1, text: "user 1 post");
        await SeedPost(userId: 2, text: "user 2 post");

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(postUser1.Id, item.Id);
    }

    [Fact]
    public async Task GetProfileFeed_MapsAllFieldsCorrectly()
    {
        // Arrange
        await SeedUser(userId: 1, email: "alice@example.com");
        var updated = DateTimeOffset.UtcNow;
        var post = await SeedPost(
            userId: 1,
            text: "full post",
            imageUrl: "https://example.com/pic.png",
            updatedAt: updated);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("full post", item.Text);
        Assert.Equal("https://example.com/pic.png", item.ImageUrl);
        Assert.Equal(post.CreatedAt, item.CreatedAt);
        Assert.Equal(updated, item.UpdatedAt);
        Assert.Equal(1, item.Author.Id);
        Assert.Equal("alice@example.com", item.Author.Email);
        Assert.Null(item.ParentPost);
    }

    [Fact]
    public async Task Delete_NonExistentPostId_ThrowsPostNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Delete(postId: 9999, userId: 1));
    }

    [Fact]
    public async Task Delete_DifferentUser_ThrowsForbiddenException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "user 1 post");

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.Delete(postId: post.Id, userId: 2));
    }

    [Fact]
    public async Task Delete_ValidPostAndOwner_SoftDeletesPostAndSetsDeletedAt()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        var post = await SeedPost(userId: 1, text: "user 1 post");

        // Act
        await _sut.Delete(postId: post.Id, userId: 1);

        // Assert
        var dbPost = await _applicationContext.Posts
            .IgnoreQueryFilters()
            .SingleAsync(p => p.Id == post.Id);

        Assert.True(dbPost.IsDeleted);
        Assert.NotNull(dbPost.DeletedAt);
        Assert.True(dbPost.DeletedAt >= before);
    }

    [Fact]
    public async Task Delete_AlreadyDeletedPost_ThrowsPostNotFoundException()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "user 1 post");
        await _sut.Delete(postId: post.Id, userId: 1);

        // Act & Assert
        await Assert.ThrowsAsync<PostNotFoundException>(
            () => _sut.Delete(postId: post.Id, userId: 1));
    }

    [Fact]
    public async Task Delete_SoftDeletedPost_ExcludedFromProfileFeed()
    {
        // Arrange
        var post1 = await SeedPost(userId: 1, text: "active post");
        var post2 = await SeedPost(userId: 1, text: "deleted post");
        await _sut.Delete(postId: post2.Id, userId: 1);

        // Act
        var feed = await _sut.GetProfileFeed(userId: 1);

        // Assert
        var item = Assert.Single(feed.Items);
        Assert.Equal(post1.Id, item.Id);
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
        var existing = await _applicationContext.Users.FindAsync(userId);
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