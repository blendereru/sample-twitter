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

    public void Dispose()
    {
        _client.Dispose();
    }

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
}