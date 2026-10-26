using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

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
        Assert.Equal(post.Id, body.Id);
        Assert.Equal("updated text", body.Text);
        Assert.Null(body.ImageUrl);
        Assert.NotNull(body.UpdatedAt);
    }

    [Fact]
    public async Task ValidEdit_PersistsChangesInDatabase()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: user.Id, text: "original");

        // Act
        var response = await SendEditRequest(cookie, post.Id,
            new EditPostRequest { Text = "persisted update", ImageUrl = "https://example.com/picture.png" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var updated = db.Posts.Single(p => p.Id == post.Id);

        Assert.Equal("persisted update", updated.Text);
        Assert.Equal("https://example.com/picture.png", updated.ImageUrl);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(user.Id, updated.UserId);
        Assert.Equal(post.CreatedAt.ToUnixTimeMilliseconds(), updated.CreatedAt.ToUnixTimeMilliseconds());
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

    private Task<HttpResponseMessage> SendEditRequest(string cookie, long postId, EditPostRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{postId}")
        {
            Content = JsonContent.Create(request),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}