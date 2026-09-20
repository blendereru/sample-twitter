using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class GetByIdTests : IntegrationTestBase
{
    public GetByIdTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidPost_Returns200WithAllFieldsMapped()
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
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.Id);
        Assert.Equal("full post content", body.Text);
        Assert.Equal("https://example.com/pic.png", body.ImageUrl);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), body.CreatedAt.ToUnixTimeMilliseconds());
        Assert.NotNull(body.UpdatedAt);
        Assert.Equal(post.UpdatedAt!.Value.ToUnixTimeMilliseconds(), body.UpdatedAt.Value.ToUnixTimeMilliseconds());
        Assert.Equal(user.Id, body.Author.Id);
        Assert.Equal("alice@example.com", body.Author.Email);
        Assert.Null(body.ReplyId);
        Assert.Equal(0, body.RepostCount);
        Assert.Equal(0, body.ReplyCount);
    }

    [Fact]
    public async Task NonExistentId_Returns404ProblemDetails()
    {
        // Arrange
        const long nonExistentId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/posts/{nonExistentId}");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidIdFormat_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/posts/not-a-number");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SoftDeletedPost_Returns404()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "will be deleted");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", cookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostWithReplies_ReturnsCorrectReplyCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var replier = await SeedUser("replier@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "root post");
        await SeedPost(userId: replier.Id, text: "reply 1", replyId: post.Id);
        await SeedPost(userId: replier.Id, text: "reply 2", replyId: post.Id);
        await SeedPost(userId: author.Id, text: "self reply", replyId: post.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.ReplyCount);
    }

    [Fact]
    public async Task PostWithReposts_ReturnsCorrectRepostCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var user1 = await SeedUser("user1@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "popular post");
        await SeedRepost(userId: user1.Id, postId: post.Id);
        await SeedRepost(userId: user2.Id, postId: post.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.RepostCount);
    }

    [Fact]
    public async Task ReplyPost_ReturnsReplyId()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(userId: user.Id, text: "parent post");
        var reply = await SeedPost(userId: user.Id, text: "reply post", replyId: parent.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{reply.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(parent.Id, body.ReplyId);
    }

    [Fact]
    public async Task RootPost_ReplyIdIsNull()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "root post");

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Null(body.ReplyId);
    }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "public post");

        // Act (no auth cookie)
        var response = await Client.GetAsync($"/api/posts/{post.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.Id);
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

    private async Task<Repost> SeedRepost(long userId, long postId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var repost = new Repost { PostId = postId, UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        db.Reposts.Add(repost);
        await db.SaveChangesAsync();
        return repost;
    }
}