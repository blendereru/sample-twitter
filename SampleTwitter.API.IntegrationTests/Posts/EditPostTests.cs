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

public class EditPostTests : IntegrationTestBase
{
    public EditPostTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidEdit_Returns200WithEditPostResponse()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: await GetUserId(cookie), text: "original text");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "updated text" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<EditPostResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    [Fact]
    public async Task ValidEdit_PersistsChangesInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "persisted update", ImageUrl = null });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("persisted update", updated.Text);
        Assert.Null(updated.ImageUrl);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(user.Id, updated.UserId);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), updated.CreatedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ValidEdit_TextWithWhitespace_IsTrimmedAndPersisted()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "  trimmed  " });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("trimmed", updated.Text);
    }

    [Fact]
    public async Task ValidEdit_WithValidImageUrl_PersistsImageUrlInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");
        const string newImageUrl = "https://example.com/picture.png";

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "updated text", ImageUrl = newImageUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("updated text", updated.Text);
        Assert.Equal(newImageUrl, updated.ImageUrl);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(user.Id, updated.UserId);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), updated.CreatedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task ValidEdit_ImageOnlyNoText_Returns200AndPersists()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original text");
        const string newImageUrl = "https://example.com/photo.png";

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = null, ImageUrl = newImageUrl });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Null(updated.Text);
        Assert.Equal(newImageUrl, updated.ImageUrl);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_RemoveExistingImage_PersistsNullImageInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original text", imageUrl: "https://example.com/old.png");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "original text", ImageUrl = null });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Null(updated.ImageUrl);
        Assert.Equal("original text", updated.Text);
    }

    [Fact]
    public async Task ValidEdit_ReplyPost_PreservesReplyId()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var parentPost = await SeedPost(userId: user.Id, text: "parent post");
        var replyPost = await SeedPost(userId: user.Id, text: "reply post", replyId: parentPost.Id);

        // Act
        var response = await SendEditRequest(cookie, replyPost.Id,
            new EditPostRequest { Text = "updated reply" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == replyPost.Id);

        Assert.Equal("updated reply", updated.Text);
        Assert.Equal(parentPost.Id, updated.ReplyId);
    }

    [Fact]
    public async Task ValidEdit_Exactly280Characters_Returns200AndPersists()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");
        var longText = new string('a', 280);

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = longText });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal(longText, updated.Text);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var user = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/posts/{post.Id}",
            new EditPostRequest { Text = "hacked" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DifferentUser_Returns403()
    {
        // Arrange
        var (user1, _) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user1.Id, text: "user1's post");
        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditRequest(user2Cookie, post.Id,
            new EditPostRequest { Text = "hacked by user2" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DifferentUser_DoesNotMutateOriginalPost()
    {
        // Arrange
        var (user1, _) = await SeedAndSignIn("user1@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user1.Id, text: "original");
        var (_, user2Cookie) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");

        // Act
        await SendEditRequest(user2Cookie, post.Id,
            new EditPostRequest { Text = "hacked" });

        // Assert — post is unchanged
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("original", unchanged.Text);
        Assert.Null(unchanged.UpdatedAt);
    }

    [Fact]
    public async Task NonExistentPostId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendEditRequest(cookie, postId: 9999,
            new EditPostRequest { Text = "ghost edit" });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyBody_NullTextAndNullImage_Returns400()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = null, ImageUrl = null });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmptyBody_WhitespaceTextAndNullImage_Returns400()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "   ", ImageUrl = null });

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FailedEdit_EmptyBody_DoesNotMutateOriginalPost()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original untouched");

        // Act
        await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = null, ImageUrl = null });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("original untouched", unchanged.Text);
        Assert.Null(unchanged.UpdatedAt);
    }

    [Fact]
    public async Task TextExceeds280Characters_Returns400WithTextValidationError()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = new string('x', 281) });

        // Assert
        await response.AssertValidationProblemDetails("Text");
    }

    [Fact]
    public async Task FailedEdit_TextExceeds280Characters_DoesNotMutateOriginalPost()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original untouched");

        // Act
        await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = new string('x', 281) });

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var unchanged = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("original untouched", unchanged.Text);
        Assert.Null(unchanged.UpdatedAt);
    }

    [Fact]
    public async Task InvalidImageUrl_Returns400WithImageUrlValidationError()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "valid text", ImageUrl = "not-a-url" });

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
        var cookie = setCookieHeader.Split(';')[0]; // "SampleTwitter.Auth=<value>"

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

    private Task<HttpResponseMessage> SendEditRequest(string cookie, long postId, EditPostRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{postId}")
        {
            Content = JsonContent.Create(request),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private async Task<long> GetUserId(string cookie)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/account/me")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        return body!.Id;
    }

}