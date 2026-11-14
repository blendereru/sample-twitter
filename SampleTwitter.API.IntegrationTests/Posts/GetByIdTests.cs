using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class GetByIdTests : IntegrationTestBase
{
    public GetByIdTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidPost_Returns200WithAllFieldsMapped()
    {
        // Arrange
        var user = await SeedUser("alice@example.com", "Sup3rSecret1!", username: "alice");
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
        Assert.Equal("alice", body.Author.Username);
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
        Assert.NotNull(body.ReplyToAuthor);
        Assert.Equal(user.Id, body.ReplyToAuthor.Id);
        Assert.Equal(user.Username, body.ReplyToAuthor.Username);
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

    [Fact]
    public async Task QuotePost_ReturnsQuoteIdAndQuotedPostDetails()
    {
        // Arrange
        var originalAuthor = await SeedUser("original@example.com", "Sup3rSecret1!", username: "original_author");
        var quoter = await SeedUser("quoter@example.com", "Sup3rSecret1!", username: "quoter_user");
        var quoted = await SeedPost(
            userId: originalAuthor.Id,
            text: "quoted content",
            imageUrl: "https://example.com/quoted.png");
        var quotePost = await SeedPost(
            userId: quoter.Id,
            text: "check this out",
            quoteId: quoted.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{quotePost.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(quoted.Id, body.QuoteId);
        Assert.NotNull(body.QuotedPost);
        Assert.Equal(quoted.Id, body.QuotedPost.Id);
        Assert.Equal("quoted content", body.QuotedPost.Text);
        Assert.Equal("https://example.com/quoted.png", body.QuotedPost.ImageUrl);
        Assert.Equal(originalAuthor.Id, body.QuotedPost.Author.Id);
        Assert.Equal("original_author", body.QuotedPost.Author.Username);
    }

    [Fact]
    public async Task QuotePost_WhenQuotedPostIsSoftDeleted_ReturnsQuoteIdWithNullQuotedPost()
    {
        // Arrange
        var (originalAuthor, originalCookie) = await SeedAndSignIn("original@example.com", "Sup3rSecret1!", username: "original_author");
        var quoter = await SeedUser("quoter@example.com", "Sup3rSecret1!", username: "quoter_user");
        var quoted = await SeedPost(userId: originalAuthor.Id, text: "will be deleted");
        var quotePost = await SeedPost(userId: quoter.Id, text: "quoting before deletion", quoteId: quoted.Id);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{quoted.Id}")
        {
            Headers = { { "Cookie", originalCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{quotePost.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(body);
        Assert.Equal(quoted.Id, body.QuoteId);
        Assert.Null(body.QuotedPost);
    }
}