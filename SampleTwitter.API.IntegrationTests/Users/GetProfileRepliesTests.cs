using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetProfileRepliesTests : IntegrationTestBase
{
    public GetProfileRepliesTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InvalidUserIdFormat_Returns404()
    {
        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/users/not-a-valid-id/replies");
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act (no auth cookie)
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedViewer_Returns200()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}/replies")
        {
            Headers = { { "Cookie", cookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        const long nonExistentUserId = 999999;

        // Act
        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{nonExistentUserId}/replies")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ZeroUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/0/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NegativeUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/-1/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DecimalUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/1.5/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OverflowUserId_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/99999999999999999999999999/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MaxLongUserId_NonExistent_Returns404()
    {
        // Act
        var response = await Client.GetAsync($"/api/users/{long.MaxValue}/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedOwnParentPost_ReplyStillExistsInFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Own post");
        var reply = await SeedPost(author.Id, "Reply to own post", replyId: post.Id);
        var deletePostRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deletePostResponse = await Client.SendAsync(deletePostRequest);
        deletePostResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();

        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(reply.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task SoftDeletedReply_ReplyNotPresentInFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Own post");
        var reply = await SeedPost(author.Id, "Reply to own post", replyId: post.Id);
        var deletePostRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{reply.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deletePostResponse = await Client.SendAsync(deletePostRequest);
        deletePostResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();

        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task UserRootPosts_ExcludedFromRepliesFeed()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var otherUser = await SeedUser("other@example.com", "Sup3rSecret1!");

        // Root post by user (must NOT appear in replies feed)
        await SeedPost(user.Id, "User root post");

        // Reply by user to other user's post (must appear in replies feed)
        var otherPost = await SeedPost(otherUser.Id, "Other user post");
        var userReply = await SeedPost(user.Id, "User reply", replyId: otherPost.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();

        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(userReply.Id, item.Id);
    }

    [Fact]
    public async Task OtherUserReplies_ExcludedFromFeed()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var feedUser = await SeedUser("signed@example.com", "Sup3rSecret1!");

        var post = await SeedPost(user.Id, "Post");
        const int otherUserRepliesCount = 2;
        for (var i = 0; i < otherUserRepliesCount; i++)
        {
            await SeedPost(user.Id, $"Reply {i} to post", replyId: post.Id);
        }

        var feedUserReply = await SeedPost(feedUser.Id, "Author reply", replyId: post.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{feedUser.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);

        Assert.Single(body.Items);
        Assert.Equal(feedUserReply.Id, body.Items[0].Id);
    }

    [Fact]
    public async Task UserExistsWithNoReplies_Returns200WithEmptyList()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }
}