using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetProfilePostsTests : IntegrationTestBase
{
    public GetProfilePostsTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var user = await SeedUser("public@example.com", "Sup3rSecret1!");
        await SeedPost(userId: user.Id, text: "public post");

        // Act (no auth cookie)
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task AuthenticatedViewer_Returns200()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        await SeedPost(userId: author.Id, text: "author post");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}/posts")
        {
            Headers = { { "Cookie", cookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task InvalidUserIdFormat_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/not-a-valid-id/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const long nonExistentUserId = 999999;

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{nonExistentUserId}/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedPost_ExcludedFromFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        const int authorPostNumbers = 4;
        var authorPosts = new Post[authorPostNumbers];

        for (var i = 0; i < authorPostNumbers; i++)
        {
            var text = $"TestPost{i}";
            authorPosts[i] = await SeedPost(author.Id, text);
        }

        var deletePostRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{authorPosts[2].Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deletePostResponse = await Client.SendAsync(deletePostRequest);
        deletePostResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();

        Assert.NotNull(body);
        Assert.Equal(authorPostNumbers - 1, body.Items.Count);
        Assert.DoesNotContain(body.Items, p => p.Id == authorPosts[2].Id);
    }

    [Fact]
    public async Task UserExistsWithNoPosts_Returns200WithEmptyList()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task UserHasPosts_Returns200WithPostsInDescendingChronologicalOrder()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        var post1 = await SeedPost(userId: user.Id, text: "older post", createdAt: t1);
        var post2 = await SeedPost(userId: user.Id, text: "newer post", createdAt: t2);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal(post2.Id, body.Items[0].Id);
        Assert.Equal(post1.Id, body.Items[1].Id);
    }

    [Fact]
    public async Task SameCreatedAt_OrdersByDescendingId()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var timestamp = DateTimeOffset.UtcNow;
        var post1 = await SeedPost(userId: user.Id, text: "post 1", createdAt: timestamp);
        var post2 = await SeedPost(userId: user.Id, text: "post 2", createdAt: timestamp);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal(post2.Id, body.Items[0].Id);
        Assert.Equal(post1.Id, body.Items[1].Id);
    }

    [Fact]
    public async Task PostReplies_ExcludedFromFeed()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");

        var replyUser = await SeedUser("user@example.com", "Sup3rSecret1!");

        // User replying to his own post
        var ownPost = await SeedPost(user.Id, "Own post");
        await SeedPost(user.Id, "Own post reply", replyId: ownPost.Id);

        // User replying to another user's post
        var replyUserPost = await SeedPost(replyUser.Id, "Reply user post");
        await SeedPost(user.Id, "Reply user post reply", replyId: replyUserPost.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert - only user's own post is included
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();

        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(ownPost.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task PostReposts_ExcludedFromFeed()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");

        var repostUser = await SeedUser("user@example.com", "Sup3rSecret1!");

        // User reposting his own post
        var ownPost = await SeedPost(user.Id, "Own post");
        await SeedRepost(user.Id, ownPost.Id);

        // User reposting another user's post
        var repostUserPost = await SeedPost(repostUser.Id, "Reply user post");
        await SeedRepost(user.Id, repostUserPost.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();

        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(ownPost.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task MapsAllFieldsCorrectly()
    {
        // Arrange
        var user = await SeedUser("alice@example.com", "Sup3rSecret1!");
        var now = DateTimeOffset.UtcNow;
        var post = await SeedPost(
            userId: user.Id,
            text: "full post content",
            imageUrl: "https://example.com/pic.png",
            createdAt: now.AddMinutes(-10),
            updatedAt: now);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);

        Assert.Equal(user.Id, body.Author.Id);
        Assert.Equal("alice@example.com", body.Author.Email);

        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("full post content", item.Text);
        Assert.Equal("https://example.com/pic.png", item.ImageUrl);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), item.CreatedAt.ToUnixTimeMilliseconds());
        Assert.NotNull(item.UpdatedAt);
        Assert.Equal(post.UpdatedAt!.Value.ToUnixTimeMilliseconds(), item.UpdatedAt.Value.ToUnixTimeMilliseconds());
        Assert.Equal(0, item.RepostCount);
        Assert.Equal(0, item.ReplyCount);
    }

    [Fact]
    public async Task OtherUsersPosts_ExcludedFromFeed()
    {
        // Arrange
        var userA = await SeedUser("userA@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userB@example.com", "Sup3rSecret1!");

        var postA1 = await SeedPost(userA.Id, "Post A1");
        var postA2 = await SeedPost(userA.Id, "Post A2");
        await SeedPost(userB.Id, "Post B1");
        await SeedPost(userB.Id, "Post B2");

        // Act
        var response = await Client.GetAsync($"/api/users/{userA.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        Assert.All(body.Items, item => Assert.True(item.Id == postA1.Id || item.Id == postA2.Id));
    }

    [Fact]
    public async Task OtherUsersInteractions_DoNotInjectItemsIntoFeed()
    {
        // Arrange
        var author = await SeedUser("author_interactions@example.com", "Sup3rSecret1!");
        var otherUser = await SeedUser("other_interactions@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Original post");
        await SeedReply(otherUser.Id, post.Id, "Other user reply");
        await SeedRepost(otherUser.Id, post.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal(1, item.ReplyCount);
        Assert.Equal(1, item.RepostCount);
    }

    [Fact]
    public async Task NonZeroCounts_MappedCorrectly()
    {
        // Arrange
        var author = await SeedUser("author_counts@example.com", "Sup3rSecret1!");
        var u1 = await SeedUser("u1_counts@example.com", "Sup3rSecret1!");
        var u2 = await SeedUser("u2_counts@example.com", "Sup3rSecret1!");
        var u3 = await SeedUser("u3_counts@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Post with interactions");
        await SeedReply(u1.Id, post.Id, "Reply 1");
        await SeedReply(u2.Id, post.Id, "Reply 2");
        await SeedReply(u3.Id, post.Id, "Reply 3");

        await SeedRepost(u1.Id, post.Id);
        await SeedRepost(u2.Id, post.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(3, item.ReplyCount);
        Assert.Equal(2, item.RepostCount);
    }

    [Fact]
    public async Task SoftDeletedReplies_ExcludedFromReplyCount()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_softdel@example.com", "Sup3rSecret1!");
        var (replier, replierCookie) = await SeedAndSignIn("replier_softdel@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Active post");
        await SeedPost(replier.Id, "Active reply", replyId: post.Id);
        var replyToDelete = await SeedPost(replier.Id, "Reply to delete", replyId: post.Id);

        var deleteMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{replyToDelete.Id}")
        {
            Headers = { { "Cookie", replierCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteMessage);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(1, item.ReplyCount);
    }

    [Fact]
    public async Task UndoingRepost_DecrementsRepostCount()
    {
        // Arrange
        var author = await SeedUser("author_undorepost@example.com", "Sup3rSecret1!");
        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_undorepost@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Post to repost");
        await SeedRepost(reposter.Id, post.Id);

        // Verify count before undo
        var initialResponse = await Client.GetAsync($"/api/users/{author.Id}/posts");
        var initialBody = await initialResponse.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.Equal(1, Assert.Single(initialBody!.Items).RepostCount);

        // Undo repost
        var undoRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var undoResponse = await Client.SendAsync(undoRequest);
        undoResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, Assert.Single(body.Items).RepostCount);
    }

    [Fact]
    public async Task NestedReplies_CountOnlyDirectReplies()
    {
        // Arrange
        var author = await SeedUser("author_nested@example.com", "Sup3rSecret1!");
        var user1 = await SeedUser("user1_nested@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2_nested@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Root post");
        var directReply = await SeedPost(user1.Id, "Direct reply", replyId: post.Id);
        await SeedPost(user2.Id, "Nested reply to reply", replyId: directReply.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(1, item.ReplyCount);
    }

    [Fact]
    public async Task EditedPost_PreservesCreatedAtOrder()
    {
        // Arrange
        var user = await SeedUser("author_editedorder@example.com", "Sup3rSecret1!");
        var t1 = DateTimeOffset.UtcNow.AddHours(-3);
        var t2 = DateTimeOffset.UtcNow.AddHours(-2);
        var t3 = DateTimeOffset.UtcNow.AddHours(-1);

        var olderPost = await SeedPost(user.Id, "Older post edited later", createdAt: t1, updatedAt: t3);
        var newerPost = await SeedPost(user.Id, "Newer post", createdAt: t2);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert - Newer post should still be first because ordering is by CreatedAt DESC
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        Assert.Equal(newerPost.Id, body.Items[0].Id);
        Assert.Equal(olderPost.Id, body.Items[1].Id);
    }

    [Fact]
    public async Task UserExistsWithNoPosts_ReturnsAuthorDetailsWithEmptyItems()
    {
        // Arrange
        var user = await SeedUser("emptyuser_details@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Author.Id);
        Assert.Equal("emptyuser_details@example.com", body.Author.Email);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task TextOnlyPost_MapsCorrectly()
    {
        // Arrange
        var user = await SeedUser("textonly@example.com", "Sup3rSecret1!");
        var post = await SeedPost(user.Id, text: "Just plain text", imageUrl: null);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("Just plain text", item.Text);
        Assert.Null(item.ImageUrl);
    }

    [Fact]
    public async Task ImageOnlyPost_MapsCorrectly()
    {
        // Arrange
        var user = await SeedUser("imageonly@example.com", "Sup3rSecret1!");
        var post = await SeedPost(user.Id, text: null, imageUrl: "https://example.com/photo.jpg");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Null(item.Text);
        Assert.Equal("https://example.com/photo.jpg", item.ImageUrl);
    }

    [Fact]
    public async Task MaxLengthText_MapsCorrectly()
    {
        // Arrange
        var user = await SeedUser("maxlength@example.com", "Sup3rSecret1!");
        var maxText = new string('x', 280);
        var post = await SeedPost(user.Id, text: maxText);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(280, item.Text!.Length);
        Assert.Equal(maxText, item.Text);
    }

    [Fact]
    public async Task UnicodeAndEmojiText_MapsCorrectly()
    {
        // Arrange
        var user = await SeedUser("emoji@example.com", "Sup3rSecret1!");
        const string unicodeText = "Hello world! 🚀 🌟 💻\nLine 2 with special chars: привет мир! ©®™";
        var post = await SeedPost(user.Id, text: unicodeText);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(unicodeText, item.Text);
    }

    [Fact]
    public async Task ZeroUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer_zero@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/0/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NegativeUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer_neg@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/-1/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DecimalUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer_dec@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/1.5/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OverflowUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer_overflow@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/99999999999999999999999999/posts")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnconfirmedEmailUser_PostsCanBeViewed()
    {
        // Arrange
        var unconfirmedUser = await SeedUser("unconfirmed@example.com", "Sup3rSecret1!", emailConfirmed: false);
        var post = await SeedPost(unconfirmedUser.Id, "Post by unconfirmed user");

        // Act
        var response = await Client.GetAsync($"/api/users/{unconfirmedUser.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
    }

    [Fact]
    public async Task ComplexProfileActivity_OnlyActiveRootPostsReturnedWithAccurateCounts()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("complex_author@example.com", "Sup3rSecret1!");
        var otherUser = await SeedUser("complex_other@example.com", "Sup3rSecret1!");

        var t1 = DateTimeOffset.UtcNow.AddHours(-5);
        var t2 = DateTimeOffset.UtcNow.AddHours(-4);
        var t3 = DateTimeOffset.UtcNow.AddHours(-3);
        var t4 = DateTimeOffset.UtcNow.AddHours(-2);
        var t5 = DateTimeOffset.UtcNow.AddHours(-1);

        // 1. Root Post 1 (older, will be edited)
        var post1 = await SeedPost(author.Id, "Post 1 original", createdAt: t1, updatedAt: t5);

        // 2. Root Post 2 (newer)
        var post2 = await SeedPost(author.Id, "Post 2", createdAt: t2);

        // 3. Post to be soft-deleted by author
        var post3 = await SeedPost(author.Id, "Post 3 to delete", createdAt: t3);
        var deleteMessage = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post3.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteMessage);
        deleteResponse.EnsureSuccessStatusCode();

        // 4. Other user's post
        var otherPost = await SeedPost(otherUser.Id, "Other user's post", createdAt: t3);

        // 5. Author self-reply to Post 1 (should NOT be in feed items, but counts towards post1 ReplyCount)
        await SeedPost(author.Id, "Author self-reply to post 1", replyId: post1.Id, createdAt: t4);

        // 6. Author reply to other user's post (should NOT be in feed items)
        await SeedPost(author.Id, "Author reply to other", replyId: otherPost.Id, createdAt: t4);

        // 7. Other user replies to Post 1
        await SeedPost(otherUser.Id, "Other user reply to post 1", replyId: post1.Id, createdAt: t4);

        // 8. Other user reposts Post 1
        await SeedRepost(otherUser.Id, post1.Id);

        // 9. Author reposts their own Post 2 (should NOT inject a duplicate feed item, but increments RepostCount)
        await SeedRepost(author.Id, post2.Id);

        // 10. Author reposts other user's post (should NOT inject a feed item)
        await SeedRepost(author.Id, otherPost.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);

        // Only post2 and post1 should appear, ordered by CreatedAt descending (post2 at t2, post1 at t1)
        Assert.Equal(2, body.Items.Count);

        var first = body.Items[0];
        Assert.Equal(post2.Id, first.Id);
        Assert.Equal(1, first.RepostCount);
        Assert.Equal(0, first.ReplyCount);

        var second = body.Items[1];
        Assert.Equal(post1.Id, second.Id);
        Assert.Equal(1, second.RepostCount);
        Assert.Equal(2, second.ReplyCount); // author self-reply + other user reply
    }

    private async Task<User> SeedUser(string email, string password, bool emailConfirmed = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailConfirmed = emailConfirmed,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(User User, string Cookie)> SeedAndSignIn(string email, string password)
    {
        var user = await SeedUser(email, password);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/signin",
            new LoginRequest { Email = email, Password = password });

        loginResponse.EnsureSuccessStatusCode();

        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith("SampleTwitter.Auth="));
        var cookie = setCookieHeader.Split(';')[0];

        return (user, cookie);
    }

    private async Task<Post> SeedPost(
        long userId,
        string? text = null,
        string? imageUrl = null,
        long? replyId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private async Task<Post> SeedReply(long userId, long replyId, string text)
    {
        return await SeedPost(userId: userId, text: text, replyId: replyId);
    }

    private async Task<Repost> SeedRepost(long userId, long postId, DateTimeOffset? createdAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var repost = new Repost { PostId = postId, UserId = userId, CreatedAt = createdAt ?? DateTimeOffset.UtcNow };

        db.Reposts.Add(repost);
        await db.SaveChangesAsync();
        return repost;
    }
}