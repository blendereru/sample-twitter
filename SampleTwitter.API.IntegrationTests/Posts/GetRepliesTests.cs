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

public class GetRepliesTests : IntegrationTestBase
{
    public GetRepliesTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task NonExistentPostId_Returns404WithProblemDetails()
    {
        // Act
        var response = await Client.GetAsync("/api/posts/999999/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedPostId_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_soft_replies@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to be soft deleted");

        var deleteResponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostWithNoReplies_Returns200WithEmptyList()
    {
        // Arrange
        var author = await SeedUser("author_noreplies@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post with no replies");

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task PostWithReplies_Returns200WithRepliesOrderedChronologically()
    {
        // Arrange
        var author = await SeedUser("author_thread@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "main discussion post");

        var user1 = await SeedUser("user1_reply@example.com", "Sup3rSecret1!");
        var user2 = await SeedUser("user2_reply@example.com", "Sup3rSecret1!");

        var t1 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-1);

        var reply1 = await SeedPost(userId: user1.Id, text: "first response", replyId: post.Id, createdAt: t1);
        var reply2 = await SeedPost(userId: user2.Id, text: "second response", replyId: post.Id, createdAt: t2);

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);

        Assert.Equal(reply1.Id, body.Items[0].Id);
        Assert.Equal("first response", body.Items[0].Text);
        Assert.Equal(user1.Id, body.Items[0].Author.Id);

        Assert.Equal(reply2.Id, body.Items[1].Id);
        Assert.Equal("second response", body.Items[1].Text);
        Assert.Equal(user2.Id, body.Items[1].Author.Id);
    }

    [Fact]
    public async Task UnauthenticatedViewer_CanRetrieveReplies()
    {
        // Arrange
        var author = await SeedUser("author_unauth_replies@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "public post");
        var replier = await SeedUser("replier_unauth@example.com", "Sup3rSecret1!");
        await SeedPost(userId: replier.Id, text: "public reply", replyId: post.Id);

        // Act (using unauthenticated Client without cookie)
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task RepliesIncludeRepostAndReplyCounts()
    {
        // Arrange
        var author = await SeedUser("author_counts@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for counts test");

        var replier = await SeedUser("replier_counts@example.com", "Sup3rSecret1!");
        var reply = await SeedPost(userId: replier.Id, text: "reply to be engaged with", replyId: post.Id);

        var reposter = await SeedUser("reposter_of_reply@example.com", "Sup3rSecret1!");
        await SeedRepost(postId: reply.Id, userId: reposter.Id);

        var nestedReplier = await SeedUser("nested_replier@example.com", "Sup3rSecret1!");
        await SeedPost(userId: nestedReplier.Id, text: "nested reply", replyId: reply.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);

        var item = Assert.Single(body.Items);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(1, item.RepostCount);
        Assert.Equal(1, item.ReplyCount);
    }

    [Fact]
    public async Task SoftDeletedReplies_ExcludedFromRepliesList()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_del_replies@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post with active and deleted replies");

        var (activeUser, _) = await SeedAndSignIn("active_replier@example.com", "Sup3rSecret1!");
        var activeReply = await SeedPost(userId: activeUser.Id, text: "still here", replyId: post.Id);

        var (delUser, delCookie) = await SeedAndSignIn("del_replier@example.com", "Sup3rSecret1!");
        var deletedReply = await SeedPost(userId: delUser.Id, text: "soon gone", replyId: post.Id);

        var deleteResponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{deletedReply.Id}")
        {
            Headers = { { "Cookie", delCookie } }
        });
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(activeReply.Id, item.Id);
    }

    [Fact]
    public async Task InvalidPostIdFormat_Returns400Or404()
    {
        // Act
        var response = await Client.GetAsync("/api/posts/not-a-number/replies");

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
        DateTimeOffset? createdAt = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var post = new Post
        {
            Text = text,
            ImageUrl = imageUrl,
            ReplyId = replyId,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    private async Task SeedRepost(long postId, long userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var repost = new Repost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Reposts.Add(repost);
        await db.SaveChangesAsync();
    }
}