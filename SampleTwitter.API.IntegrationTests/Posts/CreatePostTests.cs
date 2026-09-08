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

public class CreatePostTests : IntegrationTestBase
{
    public CreatePostTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidCreate_TextOnly_Returns201WithCreatePostResponse()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "hello world" };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);
        Assert.True(body.PostId > 0);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task ValidCreate_TextOnly_SetsLocationHeader()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "location test" };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);
        Assert.EndsWith($"/api/posts/{body.PostId}", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task ValidCreate_TextOnly_PersistsInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "persisted text" };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal("persisted text", post.Text);
        Assert.Null(post.ImageUrl);
        Assert.Null(post.ReplyId);
        Assert.Equal(user.Id, post.UserId);
        Assert.Null(post.UpdatedAt);
        Assert.True(post.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ValidCreate_ImageOnlyNoText_Returns201AndPersists()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string imageUrl = "https://example.com/photo.png";
        var request = new CreatePostRequest { Text = null, ImageUrl = imageUrl };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Null(post.Text);
        Assert.Equal(imageUrl, post.ImageUrl);
        Assert.Null(post.ReplyId);
        Assert.Equal(user.Id, post.UserId);
    }

    [Fact]
    public async Task ValidCreate_TextAndImage_Returns201AndPersistsBoth()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const string imageUrl = "https://example.com/photo.png";
        var request = new CreatePostRequest { Text = "post with image", ImageUrl = imageUrl };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal("post with image", post.Text);
        Assert.Equal(imageUrl, post.ImageUrl);
        Assert.Equal(user.Id, post.UserId);
    }

    [Fact]
    public async Task ValidCreate_TextWithWhitespace_IsTrimmedAndPersisted()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "   trimmed content   " };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal("trimmed content", post.Text);
    }

    [Fact]
    public async Task ValidCreate_Exactly280Characters_Returns201AndPersists()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var longText = new string('a', 280);
        var request = new CreatePostRequest { Text = longText };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal(longText, post.Text);
    }

    [Fact]
    public async Task ValidCreate_ReplyToExistingPost_Returns201AndPersistsReplyId()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parentPost = await SeedPost(userId: author.Id, text: "parent post");

        var (replier, replierCookie) = await SeedAndSignIn("replier@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "reply post", ReplyId = parentPost.Id };

        // Act
        var response = await SendCreateRequest(replierCookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal("reply post", post.Text);
        Assert.Equal(parentPost.Id, post.ReplyId);
        Assert.Equal(replier.Id, post.UserId);
    }

    [Fact]
    public async Task ValidCreate_SelfThreadReply_Returns201AndPersistsReplyId()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var parentPost = await SeedPost(userId: user.Id, text: "thread root");
        var request = new CreatePostRequest { Text = "thread continuation", ReplyId = parentPost.Id };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(body);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var post = db.Posts.Single(p => p.Id == body.PostId);

        Assert.Equal("thread continuation", post.Text);
        Assert.Equal(parentPost.Id, post.ReplyId);
        Assert.Equal(user.Id, post.UserId);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "unauthenticated post" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/posts", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_DoesNotPersistPost()
    {
        // Arrange
        var request = new CreatePostRequest { Text = "unauthenticated post" };

        // Act
        await Client.PostAsJsonAsync("/api/posts", request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Posts);
    }

    [Fact]
    public async Task InvalidAuthCookie_Returns401()
    {
        // Arrange
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
        {
            Content = JsonContent.Create(new CreatePostRequest { Text = "fake auth" }),
            Headers = { { "Cookie", "SampleTwitter.Auth=invalid-token" } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBody_NullTextAndNullImage_Returns400()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = null, ImageUrl = null };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyBody_WhitespaceTextAndNullImage_Returns400()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "   ", ImageUrl = null };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FailedCreate_EmptyBody_DoesNotPersistPost()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = null, ImageUrl = null };

        // Act
        await SendCreateRequest(cookie, request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Posts);
    }

    [Fact]
    public async Task NonExistentReplyId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "reply to nowhere", ReplyId = 999999 };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FailedCreate_NonExistentReplyId_DoesNotPersistPost()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "reply to nowhere", ReplyId = 999999 };

        // Act
        await SendCreateRequest(cookie, request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Posts);
    }

    [Fact]
    public async Task TextExceeds280Characters_Returns400WithTextValidationError()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = new string('x', 281) };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertValidationProblemDetails("Text");
    }

    [Fact]
    public async Task FailedCreate_TextExceeds280Characters_DoesNotPersistPost()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = new string('x', 281) };

        // Act
        await SendCreateRequest(cookie, request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Posts);
    }

    [Fact]
    public async Task InvalidImageUrl_Returns400WithImageUrlValidationError()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "valid text", ImageUrl = "not-a-valid-url" };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertValidationProblemDetails("ImageUrl");
    }

    [Fact]
    public async Task FailedCreate_InvalidImageUrl_DoesNotPersistPost()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = "valid text", ImageUrl = "not-a-valid-url" };

        // Act
        await SendCreateRequest(cookie, request);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Posts);
    }

    [Fact]
    public async Task WhitespaceImageUrl_Returns400WithImageUrlValidationError()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var request = new CreatePostRequest { Text = null, ImageUrl = "   " };

        // Act
        var response = await SendCreateRequest(cookie, request);

        // Assert
        await response.AssertValidationProblemDetails("ImageUrl");
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

        var loginResponse = await Client.PostAsJsonAsync("/api/account/signin",
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

    private Task<HttpResponseMessage> SendCreateRequest(string cookie, CreatePostRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
        {
            Content = JsonContent.Create(request),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}