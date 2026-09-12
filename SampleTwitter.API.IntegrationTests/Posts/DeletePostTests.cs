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

public class DeletePostTests : IntegrationTestBase
{
    public DeletePostTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidDelete_Returns204NoContent()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: await GetUserId(cookie), text: "post to delete");

        // Act
        var response = await SendDeleteRequest(cookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ValidDelete_SoftDeletesPostInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var before = DateTimeOffset.UtcNow;
        var post = await SeedPost(
            userId: user.Id,
            text: "persisted post",
            imageUrl: "https://example.com/image.png");

        // Act
        var response = await SendDeleteRequest(cookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var deleted = db.Posts.IgnoreQueryFilters().Single(p => p.Id == post.Id);

        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
        Assert.True(deleted.DeletedAt >= before);
        Assert.Equal("persisted post", deleted.Text);
        Assert.Equal("https://example.com/image.png", deleted.ImageUrl);
        Assert.Equal(user.Id, deleted.UserId);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), deleted.CreatedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ValidDelete_PostIsNoLongerVisibleViaStandardQueries()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "will be filtered out");

        // Act
        await SendDeleteRequest(cookie, post.Id);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var filtered = db.Posts.SingleOrDefault(p => p.Id == post.Id);

        Assert.Null(filtered);
    }

    [Fact]
    public async Task ValidDelete_PostIsExcludedFromProfileFeed()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post1 = await SeedPost(userId: user.Id, text: "active post");
        var post2 = await SeedPost(userId: user.Id, text: "post to delete");

        // Act
        await SendDeleteRequest(cookie, post2.Id);

        var feedResponse = await Client.GetAsync($"/api/users/{user.Id}/posts");
        feedResponse.EnsureSuccessStatusCode();

        var feed = await feedResponse.Content.ReadFromJsonAsync<PostFeedResponse>();

        // Assert
        Assert.NotNull(feed);
        var item = Assert.Single(feed.Items);
        Assert.Equal(post1.Id, item.Id);
        Assert.Equal("active post", item.Text);
    }

    [Fact]
    public async Task ValidDelete_ReplyPost_SoftDeletesReplyWithoutAffectingParent()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(userId: user.Id, text: "parent post");
        var reply = await SeedPost(userId: user.Id, text: "reply post", replyId: parent.Id);

        // Act
        var response = await SendDeleteRequest(cookie, reply.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var dbParent = db.Posts.Single(p => p.Id == parent.Id);
        Assert.False(dbParent.IsDeleted);

        var dbReply = db.Posts.IgnoreQueryFilters().Single(p => p.Id == reply.Id);
        Assert.True(dbReply.IsDeleted);
        Assert.NotNull(dbReply.DeletedAt);
    }

    [Fact]
    public async Task ValidDelete_ParentPost_SoftDeletesParentWhileReplyRetainsForeignKey()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(userId: user.Id, text: "parent post");
        var reply = await SeedPost(userId: user.Id, text: "reply post", replyId: parent.Id);

        // Act
        var response = await SendDeleteRequest(cookie, parent.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope1 = Factory.Services.CreateScope())
        {
            var db = scope1.ServiceProvider.GetRequiredService<ApplicationContext>();
            var dbParent = db.Posts.IgnoreQueryFilters().Single(p => p.Id == parent.Id);
            Assert.True(dbParent.IsDeleted);
        }

        using (var scope2 = Factory.Services.CreateScope())
        {
            var db = scope2.ServiceProvider.GetRequiredService<ApplicationContext>();
            var dbReply = db.Posts.Include(p => p.Reply).Single(p => p.Id == reply.Id);
            Assert.False(dbReply.IsDeleted);
            Assert.Equal(parent.Id, dbReply.ReplyId);
            Assert.Null(dbReply.Reply);
        }
    }

    [Fact]
    public async Task AlreadyDeletedPost_SecondDeleteAttempt_Returns404()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "post to delete");

        var firstResponse = await SendDeleteRequest(cookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        // Act — attempting to delete an already soft-deleted post
        var secondResponse = await SendDeleteRequest(cookie, post.Id);

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original post");

        // Act
        var response = await Client.DeleteAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_DoesNotMutatePostInDatabase()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original post");

        // Act
        await Client.DeleteAsync($"/api/posts/{post.Id}");

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Posts.Single(p => p.Id == post.Id);

        Assert.False(unchanged.IsDeleted);
        Assert.Null(unchanged.DeletedAt);
    }

    [Fact]
    public async Task DifferentUser_Returns403()
    {
        // Arrange
        var (user1, _) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user1.Id, text: "user 1's post");
        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendDeleteRequest(user2Cookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DifferentUser_DoesNotMutatePostInDatabase()
    {
        // Arrange
        var (user1, _) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user1.Id, text: "user 1's post");
        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        await SendDeleteRequest(user2Cookie, post.Id);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal(user1.Id, unchanged.UserId);
        Assert.False(unchanged.IsDeleted);
        Assert.Null(unchanged.DeletedAt);
    }

    [Fact]
    public async Task NonExistentPostId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendDeleteRequest(cookie, postId: 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Delete, "/api/posts/not-a-valid-id")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, got {response.StatusCode}");
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

    private Task<HttpResponseMessage> SendDeleteRequest(string cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}")
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private async Task<long> GetUserId(string cookie)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        return body!.Id;
    }
}