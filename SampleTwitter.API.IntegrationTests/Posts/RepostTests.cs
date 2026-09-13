using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class RepostTests : IntegrationTestBase, IDisposable
{
    private readonly HttpClient _client;

    public RepostTests(ApiWebApplicationFactory factory) : base(factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task ValidRepost_Returns200WithRepostResponse()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "original post");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await SendRepostRequest(reposterCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RepostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.Equal(reposter.Id, body.UserId);
        Assert.True(body.CreatedAt >= before.AddSeconds(-1));
    }

    [Fact]
    public async Task ValidRepost_PersistsInDatabase()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to repost");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);

        Assert.NotNull(repost);
        Assert.Equal(post.Id, repost.PostId);
        Assert.Equal(reposter.Id, repost.UserId);
    }

    [Fact]
    public async Task ValidRepost_AuthorRepostingOwnPost_Succeeds()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "author's own post");

        // Act
        var response = await SendRepostRequest(authorCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RepostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.Equal(author.Id, body.UserId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == author.Id);
        Assert.NotNull(repost);
    }

    [Fact]
    public async Task AlreadyReposted_SecondAttempt_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to repost twice");

        var (_, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        var firstResponse = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act
        var secondResponse = await SendRepostRequest(reposterCookie, post.Id);

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AlreadyReposted_DoesNotDuplicateDatabaseRecord()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "duplicate test post");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        await SendRepostRequest(reposterCookie, post.Id);
        await SendRepostRequest(reposterCookie, post.Id);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Reposts.CountAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DifferentUsers_CanRepostSamePost_BothPersist()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "viral post");

        var (user2, cookie2) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");
        var (user3, cookie3) = await SeedAndSignIn("user3@example.com", "Sup3rSecret1!");

        // Act
        var resp2 = await SendRepostRequest(cookie2, post.Id);
        var resp3 = await SendRepostRequest(cookie3, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var reposts = await db.Reposts.Where(r => r.PostId == post.Id).ToListAsync();
        Assert.Equal(2, reposts.Count);
        Assert.Contains(reposts, r => r.UserId == user2.Id);
        Assert.Contains(reposts, r => r.UserId == user3.Id);
    }

    [Fact]
    public async Task SameUser_CanRepostMultipleDifferentPosts()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var postA = await SeedPost(userId: author.Id, text: "post A");
        var postB = await SeedPost(userId: author.Id, text: "post B");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        // Act
        var respA = await SendRepostRequest(reposterCookie, postA.Id);
        var respB = await SendRepostRequest(reposterCookie, postB.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Reposts.CountAsync(r => r.UserId == reposter.Id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "public post");

        // Act (no auth cookie)
        var response = await _client.PostAsync($"/api/posts/{post.Id}/repost", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_DoesNotMutateDatabase()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "public post");

        // Act
        await _client.PostAsync($"/api/posts/{post.Id}/repost", null);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Reposts.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task NonExistentPostId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendRepostRequest(cookie, postId: 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedPost_Returns404()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post soon deleted");

        // Author deletes post
        var deleteResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var (_, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        // Act — Attempting to repost soft-deleted post
        var response = await SendRepostRequest(reposterCookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/posts/not-a-number/repost")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await _client.SendAsync(message);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, but got {response.StatusCode}");
    }

    private async Task<User> SeedUser(string email, string password)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(password),
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(User User, string Cookie)> SeedAndSignIn(string email, string password)
    {
        var user = await SeedUser(email, password);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/signin",
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
        long? replyId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private Task<HttpResponseMessage> SendRepostRequest(string cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{postId}/repost")
        {
            Headers = { { "Cookie", cookie } }
        };
        return _client.SendAsync(message);
    }

    private Task<HttpResponseMessage> SendUndoRepostRequest(string? cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}/repost");
        if (cookie is not null)
        {
            message.Headers.Add("Cookie", cookie);
        }
        return _client.SendAsync(message);
    }

    [Fact]
    public async Task UndoRepost_ExistingRepost_Returns204AndDeletesRepost()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to undo repost on");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_undo@example.com", "Sup3rSecret1!");
        var repostResponse = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        // Act
        var undoResponse = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repostInDb = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.Null(repostInDb);
    }

    [Fact]
    public async Task UndoRepost_ExcludesRepostFromProfileFeed_AndDecrementsRepostCount()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_feed_undo@example.com", "Sup3rSecret1!");
        var authorPost = await SeedPost(userId: author.Id, text: "author post for feed undo");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_feed_undo@example.com", "Sup3rSecret1!");
        var reposterPost = await SeedPost(userId: reposter.Id, text: "reposter own post");

        var repostResponse = await SendRepostRequest(reposterCookie, authorPost.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        var feedBefore = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        feedBefore.EnsureSuccessStatusCode();
        var feedBodyBefore = await feedBefore.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feedBodyBefore);
        Assert.Equal(2, feedBodyBefore.Items.Count);
        Assert.Contains(feedBodyBefore.Items, i => i.Id == authorPost.Id && i.IsRepost);

        // Act
        var undoResponse = await SendUndoRepostRequest(reposterCookie, authorPost.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        // Assert
        var feedAfter = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        feedAfter.EnsureSuccessStatusCode();
        var feedBodyAfter = await feedAfter.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feedBodyAfter);
        var remainingItem = Assert.Single(feedBodyAfter.Items);
        Assert.Equal(reposterPost.Id, remainingItem.Id);
        Assert.False(remainingItem.IsRepost);

        var authorFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        authorFeed.EnsureSuccessStatusCode();
        var authorFeedBody = await authorFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(authorFeedBody);
        var authorItem = Assert.Single(authorFeedBody.Items);
        Assert.Equal(0, authorItem.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_SelfRepost_RemovesDuplicateRepostAndRestoresCount()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_self_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "self post");

        var repostResponse = await SendRepostRequest(authorCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        var feedBefore = await _client.GetAsync($"/api/users/{author.Id}/posts");
        feedBefore.EnsureSuccessStatusCode();
        var feedBodyBefore = await feedBefore.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feedBodyBefore);
        Assert.Equal(2, feedBodyBefore.Items.Count);

        // Act
        var undoResponse = await SendUndoRepostRequest(authorCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        // Assert
        var feedAfter = await _client.GetAsync($"/api/users/{author.Id}/posts");
        feedAfter.EnsureSuccessStatusCode();
        var feedBodyAfter = await feedAfter.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feedBodyAfter);
        var item = Assert.Single(feedBodyAfter.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.False(item.IsRepost);
        Assert.Equal(0, item.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth undo");

        // Act
        var response = await SendUndoRepostRequest(null, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UndoRepost_Unauthenticated_DoesNotMutateDatabase()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth_db@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth db test");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_unauth_db@example.com", "Sup3rSecret1!");
        await SendRepostRequest(reposterCookie, post.Id);

        // Act
        var response = await SendUndoRepostRequest(null, post.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.NotNull(repost);
    }

    [Fact]
    public async Task UndoRepost_RepostDoesNotExist_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_notreposted@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post never reposted");

        var (_, userCookie) = await SeedAndSignIn("user_neverreposted@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoRepostRequest(userCookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_NonExistentPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (_, userCookie) = await SeedAndSignIn("user_nonexistent@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoRepostRequest(userCookie, 99999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_SoftDeletedPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_soft_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to be soft deleted");

        var (_, reposterCookie) = await SeedAndSignIn("reposter_soft_undo@example.com", "Sup3rSecret1!");
        await SendRepostRequest(reposterCookie, post.Id);

        // Soft delete original post
        var deleteResp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var undoResponse = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert
        await undoResponse.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user_invalid_undo@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Delete, "/api/posts/not-a-number/repost")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await _client.SendAsync(message);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, but got {response.StatusCode}");
    }

    [Fact]
    public async Task UndoRepost_AnotherUsersRepost_Returns404WithProblemDetails_AndDoesNotDeleteRepost()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_other_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post reposted by someone else");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_other_undo@example.com", "Sup3rSecret1!");
        var repostResponse = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        var (bystander, bystanderCookie) = await SeedAndSignIn("bystander_other_undo@example.com", "Sup3rSecret1!");

        // Act — Bystander attempts to undo reposter's repost
        var undoResponse = await SendUndoRepostRequest(bystanderCookie, post.Id);

        // Assert
        await undoResponse.AssertProblemDetails(HttpStatusCode.NotFound);

        // Repost must still exist in DB for reposter
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repostInDb = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.NotNull(repostInDb);

        // Reposter feed must still contain the repost
        var reposterFeed = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        reposterFeed.EnsureSuccessStatusCode();
        var reposterFeedBody = await reposterFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(reposterFeedBody);
        var repostItem = Assert.Single(reposterFeedBody.Items);
        Assert.Equal(post.Id, repostItem.Id);
        Assert.True(repostItem.IsRepost);
        Assert.Equal(1, repostItem.RepostCount);

        // Author feed must still show repost count of 1
        var authorFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        authorFeed.EnsureSuccessStatusCode();
        var authorFeedBody = await authorFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(authorFeedBody);
        var authorItem = Assert.Single(authorFeedBody.Items);
        Assert.Equal(1, authorItem.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_PostAuthorAttemptingToUndoAnotherUsersRepost_Returns404WithProblemDetails_AndLeavesRepostIntact()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_cant_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "author post reposted by user");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_cant_undo@example.com", "Sup3rSecret1!");
        var repostResponse = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        // Act — Author attempts to undo reposter's repost (author never reposted it)
        var undoResponse = await SendUndoRepostRequest(authorCookie, post.Id);

        // Assert
        await undoResponse.AssertProblemDetails(HttpStatusCode.NotFound);

        // Repost must remain intact in DB
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repostInDb = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.NotNull(repostInDb);

        // Reposter feed still contains the repost
        var reposterFeed = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        reposterFeed.EnsureSuccessStatusCode();
        var reposterFeedBody = await reposterFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(reposterFeedBody);
        var item = Assert.Single(reposterFeedBody.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.True(item.IsRepost);
    }

    [Fact]
    public async Task UndoRepost_MultipleReposters_UndoingOneRepostLeavesOtherRepostsIntact()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_multi_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "popular post");

        var (reposter1, cookie1) = await SeedAndSignIn("reposter1_multi_undo@example.com", "Sup3rSecret1!");
        var (reposter2, cookie2) = await SeedAndSignIn("reposter2_multi_undo@example.com", "Sup3rSecret1!");

        await SendRepostRequest(cookie1, post.Id);
        await SendRepostRequest(cookie2, post.Id);

        // Verify initial state has 2 reposts
        var initialFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        initialFeed.EnsureSuccessStatusCode();
        var initialFeedBody = await initialFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(initialFeedBody);
        Assert.Equal(2, Assert.Single(initialFeedBody.Items).RepostCount);

        // Act — reposter1 undoes their repost
        var undoResponse = await SendUndoRepostRequest(cookie1, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        // Assert — reposter1's repost removed, reposter2's repost retained
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var rep1Repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter1.Id);
        var rep2Repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter2.Id);
        Assert.Null(rep1Repost);
        Assert.NotNull(rep2Repost);

        // Author feed count decremented to 1
        var authorFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        authorFeed.EnsureSuccessStatusCode();
        var authorFeedBody = await authorFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(authorFeedBody);
        Assert.Equal(1, Assert.Single(authorFeedBody.Items).RepostCount);

        // reposter1 feed no longer has the repost
        var feed1 = await _client.GetAsync($"/api/users/{reposter1.Id}/posts");
        feed1.EnsureSuccessStatusCode();
        var feed1Body = await feed1.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feed1Body);
        Assert.Empty(feed1Body.Items);

        // reposter2 feed still has the repost
        var feed2 = await _client.GetAsync($"/api/users/{reposter2.Id}/posts");
        feed2.EnsureSuccessStatusCode();
        var feed2Body = await feed2.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(feed2Body);
        var rep2Item = Assert.Single(feed2Body.Items);
        Assert.Equal(post.Id, rep2Item.Id);
        Assert.True(rep2Item.IsRepost);
        Assert.Equal(1, rep2Item.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_TwiceInSuccession_SecondAttemptReturns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_double_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for double undo");

        var (_, reposterCookie) = await SeedAndSignIn("reposter_double_undo@example.com", "Sup3rSecret1!");
        var repostResp = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResp.StatusCode);

        // Act 1 — First undo succeeds
        var firstUndo = await SendUndoRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstUndo.StatusCode);

        // Act 2 — Second undo immediately following
        var secondUndo = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert 2 — Returns 404 ProblemDetails
        await secondUndo.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_ThenRepostAgain_SucceedsAndRestoresFeedAndCount()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_re_repost@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for toggle cycle");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_re_repost@example.com", "Sup3rSecret1!");

        // Cycle: Repost -> Undo -> Repost
        var firstRepost = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, firstRepost.StatusCode);

        var undo = await SendUndoRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);

        // Act — Repost again
        var secondRepost = await SendRepostRequest(reposterCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondRepost.StatusCode);
        var body = await secondRepost.Content.ReadFromJsonAsync<RepostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.Equal(reposter.Id, body.UserId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repostInDb = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.NotNull(repostInDb);

        var reposterFeed = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        reposterFeed.EnsureSuccessStatusCode();
        var reposterFeedBody = await reposterFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(reposterFeedBody);
        var item = Assert.Single(reposterFeedBody.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.True(item.IsRepost);
        Assert.Equal(1, item.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_SelfRepostWhenAnotherUserAlsoReposted_DecrementsCountAndRemovesDuplicateFromAuthorFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_self_and_other@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "author post for co-repost test");

        var (otherUser, otherCookie) = await SeedAndSignIn("other_self_and_other@example.com", "Sup3rSecret1!");

        // Author self-reposts and other user reposts
        await SendRepostRequest(authorCookie, post.Id);
        await SendRepostRequest(otherCookie, post.Id);

        // Author feed initially has 2 items (self-repost + authored post), repost count = 2
        var initialFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        initialFeed.EnsureSuccessStatusCode();
        var initialFeedBody = await initialFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(initialFeedBody);
        Assert.Equal(2, initialFeedBody.Items.Count);

        // Act — Author undoes self-repost
        var undoResponse = await SendUndoRepostRequest(authorCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        // Assert — Author feed now only has authored post with repost count = 1 (other user's repost remaining)
        var authorFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        authorFeed.EnsureSuccessStatusCode();
        var authorFeedBody = await authorFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(authorFeedBody);
        var authorItem = Assert.Single(authorFeedBody.Items);
        Assert.Equal(post.Id, authorItem.Id);
        Assert.False(authorItem.IsRepost);
        Assert.Equal(1, authorItem.RepostCount);

        // Other user's repost is still intact
        var otherFeed = await _client.GetAsync($"/api/users/{otherUser.Id}/posts");
        otherFeed.EnsureSuccessStatusCode();
        var otherFeedBody = await otherFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(otherFeedBody);
        var otherItem = Assert.Single(otherFeedBody.Items);
        Assert.Equal(post.Id, otherItem.Id);
        Assert.True(otherItem.IsRepost);
        Assert.Equal(1, otherItem.RepostCount);
    }

    [Fact]
    public async Task UndoRepost_ThreadContinuationPost_RemovesRepostAndDecrementsChildRepostCountOnly()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_thread_undo@example.com", "Sup3rSecret1!");
        var rootPost = await SeedPost(userId: author.Id, text: "thread root");
        var replyPost = await SeedPost(userId: author.Id, text: "thread continuation", replyId: rootPost.Id);

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_thread_undo@example.com", "Sup3rSecret1!");
        var repostResp = await SendRepostRequest(reposterCookie, replyPost.Id);
        Assert.Equal(HttpStatusCode.OK, repostResp.StatusCode);

        // Act — Undo repost on the continuation post
        var undoResp = await SendUndoRepostRequest(reposterCookie, replyPost.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResp.StatusCode);

        // Assert — Reposter feed no longer has the continuation post
        var reposterFeed = await _client.GetAsync($"/api/users/{reposter.Id}/posts");
        reposterFeed.EnsureSuccessStatusCode();
        var reposterFeedBody = await reposterFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(reposterFeedBody);
        Assert.Empty(reposterFeedBody.Items);

        // Author feed: replyPost has repostCount = 0, rootPost has repostCount = 0
        var authorFeed = await _client.GetAsync($"/api/users/{author.Id}/posts");
        authorFeed.EnsureSuccessStatusCode();
        var authorFeedBody = await authorFeed.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(authorFeedBody);
        Assert.Equal(2, authorFeedBody.Items.Count);

        var replyItem = authorFeedBody.Items.First(i => i.Id == replyPost.Id);
        Assert.Equal(0, replyItem.RepostCount);
        Assert.NotNull(replyItem.ParentPost);
        Assert.Equal(0, replyItem.ParentPost.RepostCount);

        var rootItem = authorFeedBody.Items.First(i => i.Id == rootPost.Id);
        Assert.Equal(0, rootItem.RepostCount);
    }
}