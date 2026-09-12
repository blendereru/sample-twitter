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
    public async Task NonExistentUser_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/999999/posts");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
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
    public async Task SelfThreadReplies_IncludedWithParentPostContext()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var t2 = DateTimeOffset.UtcNow;
        var rootPost = await SeedPost(userId: user.Id, text: "root post", createdAt: t1);
        var replyPost = await SeedPost(userId: user.Id, text: "thread continuation", replyId: rootPost.Id, createdAt: t2);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);

        var replyItem = body.Items[0];
        Assert.Equal(replyPost.Id, replyItem.Id);
        Assert.Equal("thread continuation", replyItem.Text);
        Assert.NotNull(replyItem.ParentPost);
        Assert.Equal(rootPost.Id, replyItem.ParentPost.Id);
        Assert.Equal("root post", replyItem.ParentPost.Text);
        Assert.Equal(user.Id, replyItem.ParentPost.Author.Id);
        Assert.Equal(user.Email, replyItem.ParentPost.Author.Email);

        var rootItem = body.Items[1];
        Assert.Equal(rootPost.Id, rootItem.Id);
        Assert.Null(rootItem.ParentPost);
    }

    [Fact]
    public async Task RepliesToOtherUsers_ExcludedFromProfileFeed()
    {
        // Arrange
        var user1 = await SeedUser("user1@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2@example.com", "Sup3rSecret1!");

        var user2Post = await SeedPost(userId: user2.Id, text: "user 2 post");
        var replyToUser2 = await SeedPost(userId: user1.Id, text: "reply to user 2", replyId: user2Post.Id);
        var topLevelPost = await SeedPost(userId: user1.Id, text: "user 1 original post");

        // Act
        var response = await Client.GetAsync($"/api/users/{user1.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);

        var item = Assert.Single(body.Items);
        Assert.Equal(topLevelPost.Id, item.Id);
        Assert.DoesNotContain(body.Items, p => p.Id == replyToUser2.Id);
    }

    [Fact]
    public async Task OtherUsersPosts_ExcludedFromFeed()
    {
        // Arrange
        var user1 = await SeedUser("user1@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2@example.com", "Sup3rSecret1!");

        var postUser1 = await SeedPost(userId: user1.Id, text: "user 1 post");
        await SeedPost(userId: user2.Id, text: "user 2 post");

        // Act
        var response = await Client.GetAsync($"/api/users/{user1.Id}/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);

        var item = Assert.Single(body.Items);
        Assert.Equal(postUser1.Id, item.Id);
    }

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

        var item = Assert.Single(body.Items);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("full post content", item.Text);
        Assert.Equal("https://example.com/pic.png", item.ImageUrl);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), item.CreatedAt.ToUnixTimeMilliseconds());
        Assert.NotNull(item.UpdatedAt);
        Assert.Equal(post.UpdatedAt!.Value.ToUnixTimeMilliseconds(), item.UpdatedAt!.Value.ToUnixTimeMilliseconds());
        Assert.Equal(user.Id, item.Author.Id);
        Assert.Equal("alice@example.com", item.Author.Email);
        Assert.Null(item.ParentPost);
    }

    [Fact]
    public async Task InvalidUserIdFormat_Returns400Or404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/not-a-number/posts");

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
}