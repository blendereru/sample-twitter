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
        Assert.False(item.IsRepost);
        Assert.Null(item.RepostedBy);
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
    public async Task Repost_ValidPostAndUser_CreatesRepostAndReturnsResult()
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

        var dbRepost = await _applicationContext.Reposts
            .SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == 2);
        Assert.NotNull(dbRepost);
        Assert.Equal(post.Id, dbRepost.PostId);
        Assert.Equal(2, dbRepost.UserId);
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
    public async Task GetProfileFeed_UserHasReposts_IncludesRepostsOrderedByRepostCreatedAt()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var t1 = now.AddHours(-3);
        var t2 = now.AddHours(-2);
        var t3 = now.AddHours(-1);

        var post1 = await SeedPost(userId: 1, text: "user 1 original post", createdAt: t1);
        var post2 = await SeedPost(userId: 2, text: "user 2 original post", createdAt: t2);
        var repost = await SeedRepost(postId: post2.Id, userId: 1, createdAt: t3);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        var first = result.Items[0];
        Assert.Equal(post2.Id, first.Id);
        Assert.Equal("user 2 original post", first.Text);
        Assert.Equal(post2.CreatedAt, first.CreatedAt);
        Assert.Equal(2, first.Author.Id);
        Assert.True(first.IsRepost);
        Assert.NotNull(first.RepostedBy);
        Assert.Equal(1, first.RepostedBy!.Id);

        var second = result.Items[1];
        Assert.Equal(post1.Id, second.Id);
        Assert.Equal("user 1 original post", second.Text);
        Assert.Equal(post1.CreatedAt, second.CreatedAt);
        Assert.Equal(1, second.Author.Id);
        Assert.False(second.IsRepost);
        Assert.Null(second.RepostedBy);
    }

    [Fact]
    public async Task GetProfileFeed_UserSelfReposts_AppearsTwiceInFeed()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var t1 = now.AddHours(-3);
        var t2 = now.AddHours(-2);
        var t3 = now.AddHours(-1);

        var post1 = await SeedPost(userId: 1, text: "first post", createdAt: t1);
        var post2 = await SeedPost(userId: 1, text: "second post", createdAt: t2);
        await SeedRepost(postId: post1.Id, userId: 1, createdAt: t3);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);

        // 1st: Self-repost of post1 (at t3)
        Assert.Equal(post1.Id, result.Items[0].Id);
        Assert.True(result.Items[0].IsRepost);
        Assert.NotNull(result.Items[0].RepostedBy);
        Assert.Equal(1, result.Items[0].RepostedBy.Id);

        // 2nd: post2 (at t2)
        Assert.Equal(post2.Id, result.Items[1].Id);
        Assert.False(result.Items[1].IsRepost);
        Assert.Null(result.Items[1].RepostedBy);

        // 3rd: Original post1 (at t1)
        Assert.Equal(post1.Id, result.Items[2].Id);
        Assert.False(result.Items[2].IsRepost);
        Assert.Null(result.Items[2].RepostedBy);
    }

    [Fact]
    public async Task GetProfileFeed_RepostOfDeletedPost_ExcludedFromFeed()
    {
        // Arrange
        var post = await SeedPost(userId: 2, text: "post to be deleted");
        await SeedRepost(postId: post.Id, userId: 1);
        await _sut.Delete(postId: post.Id, userId: 2);

        // Act
        var result = await _sut.GetProfileFeed(userId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProfileFeed_OtherUsersReposts_ExcludedFromUserFeed()
    {
        // Arrange
        var post = await SeedPost(userId: 1, text: "author post");
        await SeedRepost(postId: post.Id, userId: 2);

        // Act & Assert for user 1 (should only see their original post)
        var user1Feed = await _sut.GetProfileFeed(userId: 1);
        var user1Item = Assert.Single(user1Feed.Items);
        Assert.Equal(post.Id, user1Item.Id);
        Assert.False(user1Item.IsRepost);
        Assert.Null(user1Item.RepostedBy);

        // Act & Assert for user 2 (should see the repost)
        var user2Feed = await _sut.GetProfileFeed(userId: 2);
        var user2Item = Assert.Single(user2Feed.Items);
        Assert.Equal(post.Id, user2Item.Id);
        Assert.True(user2Item.IsRepost);
        Assert.NotNull(user2Item.RepostedBy);
        Assert.Equal(2, user2Item.RepostedBy.Id);
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