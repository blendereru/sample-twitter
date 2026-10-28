using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class RepostTests : IntegrationTestBase
{
    public RepostTests(ApiWebApplicationFactory factory) : base(factory) { }

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
    public async Task ConcurrentReposts_SameUserAndPost_HandlesPostgresUniqueConstraintRaceCondition()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_race@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "race test post");

        var (_, reposterCookie) = await SeedAndSignIn("reposter_race@example.com", "Sup3rSecret1!");

        using var barrier = Factory.RepostInsertBarrier.Arm(participants: 2, timeout: TimeSpan.FromSeconds(10));

        // Act — dispatch concurrent repost requests for the exact same post from the same user
        var responses = await Task.WhenAll(
            SendRepostRequest(reposterCookie, post.Id),
            SendRepostRequest(reposterCookie, post.Id));

        // Assert — the race was actually reproduced: both requests passed the alreadyReposted check
        // and attempted the INSERT simultaneously
        Assert.Equal(2, barrier.ArrivedCount);

        // Assert — exactly one winner (200 OK) and exactly one loser (409 Conflict from unique constraint catch block)
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        // Assert — database contains exactly one repost row
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Reposts.CountAsync(r => r.PostId == post.Id);
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
        var response = await Client.PostAsync($"/api/posts/{post.Id}/repost", null);

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
        await Client.PostAsync($"/api/posts/{post.Id}/repost", null);

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
        var deleteResponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
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
        var response = await Client.SendAsync(message);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, but got {response.StatusCode}");
    }

    private Task<HttpResponseMessage> SendRepostRequest(string cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{postId}/repost")
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private Task<HttpResponseMessage> SendUndoRepostRequest(string? cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}/repost");
        if (cookie is not null)
        {
            message.Headers.Add("Cookie", cookie);
        }
        return Client.SendAsync(message);
    }

    [Fact]
    public async Task UndoRepost_ExistingRepost_Returns204AndDeletesRepost()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to undo repost on");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_undo@example.com", "Sup3rSecret1!");
        var repostResponse = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResponse.StatusCode);

        // Act
        var undoResponse = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repostInDb = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.Null(repostInDb);
    }

    [Fact]
    public async Task UndoRepost_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth undo");

        // Act
        var response = await SendUndoRepostRequest(null, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UndoRepost_Unauthenticated_DoesNotMutateDatabase()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth_db@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth db test");

        var (reposter, reposterCookie) = await SeedAndSignIn("reposter_unauth_db@example.com", "Sup3rSecret1!");
        await SendRepostRequest(reposterCookie, post.Id);

        // Act
        var response = await SendUndoRepostRequest(null, post.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var repost = await db.Reposts.SingleOrDefaultAsync(r => r.PostId == post.Id && r.UserId == reposter.Id);
        Assert.NotNull(repost);
    }

    [Fact]
    public async Task UndoRepost_RepostDoesNotExist_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_notreposted@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post never reposted");

        var (_, userCookie) = await SeedAndSignIn("user_neverreposted@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoRepostRequest(userCookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_NonExistentPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (_, userCookie) = await SeedAndSignIn("user_nonexistent@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoRepostRequest(userCookie, 99999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_SoftDeletedPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_soft_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to be soft deleted");

        var (_, reposterCookie) = await SeedAndSignIn("reposter_soft_undo@example.com", "Sup3rSecret1!");
        await SendRepostRequest(reposterCookie, post.Id);

        // Soft delete original post
        var deleteResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var undoResponse = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert
        await undoResponse.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoRepost_InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user_invalid_undo@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Delete, "/api/posts/not-a-number/repost")
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(message);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400 or 404, but got {response.StatusCode}");
    }

    [Fact]
    public async Task UndoRepost_TwiceInSuccession_SecondAttemptReturns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_double_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for double undo");

        var (_, reposterCookie) = await SeedAndSignIn("reposter_double_undo@example.com", "Sup3rSecret1!");
        var repostResp = await SendRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, repostResp.StatusCode);

        // Act 1 — First undo succeeds
        var firstUndo = await SendUndoRepostRequest(reposterCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstUndo.StatusCode);

        // Act 2 — Second undo immediately following
        var secondUndo = await SendUndoRepostRequest(reposterCookie, post.Id);

        // Assert 2 — Returns 404 ProblemDetails
        await secondUndo.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReadEndpoints_ReturnViewerSpecificIsRepostedFlag()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_repost_marker@example.com", "Sup3rSecret1!");
        var rootPost = await SeedPost(userId: author.Id, text: "root post");
        var replyPost = await SeedReply(userId: author.Id, replyId: rootPost.Id, text: "reply post");

        var (_, viewerCookie) = await SeedAndSignIn("viewer_repost_marker@example.com", "Sup3rSecret1!");

        // Viewer reposts both rootPost and replyPost
        var r1 = await SendRepostRequest(viewerCookie, rootPost.Id);
        var r2 = await SendRepostRequest(viewerCookie, replyPost.Id);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // 1. GET /api/posts/{id} as viewer -> IsReposted = true
        var getPostViewer = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{rootPost.Id}")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var postBodyViewer = await getPostViewer.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(postBodyViewer);
        Assert.True(postBodyViewer.IsReposted);

        // 2. GET /api/posts/{id} as anonymous -> IsReposted = false
        var getPostAnon = await Client.GetAsync($"/api/posts/{rootPost.Id}");
        var postBodyAnon = await getPostAnon.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(postBodyAnon);
        Assert.False(postBodyAnon.IsReposted);

        // 3. GET /api/posts/{id} as author (who did not repost) -> IsReposted = false
        var getPostAuthor = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{rootPost.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        var postBodyAuthor = await getPostAuthor.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(postBodyAuthor);
        Assert.False(postBodyAuthor.IsReposted);

        // 4. GET /api/users/{userId}/posts as viewer -> IsReposted = true
        var getProfilePostsViewer = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}/posts")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var profilePostsViewer = await getProfilePostsViewer.Content.ReadFromJsonAsync<PostFeedResponse>();
        Assert.NotNull(profilePostsViewer);
        Assert.True(Assert.Single(profilePostsViewer.Items).IsReposted);

        // 5. GET /api/users/{userId}/replies as viewer -> IsReposted = true on reply and ParentPost
        var getProfileRepliesViewer = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}/replies")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var profileRepliesViewer = await getProfileRepliesViewer.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(profileRepliesViewer);
        var singleProfileReply = Assert.Single(profileRepliesViewer.Items);
        Assert.True(singleProfileReply.IsReposted);
        Assert.NotNull(singleProfileReply.ParentPost);
        Assert.True(singleProfileReply.ParentPost.IsReposted);

        // 6. GET /api/posts/{id}/replies as viewer -> IsReposted = true
        var getRepliesViewer = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{rootPost.Id}/replies")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var repliesViewer = await getRepliesViewer.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(repliesViewer);
        Assert.True(Assert.Single(repliesViewer).IsReposted);

        // 7. Undo repost on rootPost -> IsReposted becomes false
        var undoResp = await SendUndoRepostRequest(viewerCookie, rootPost.Id);
        Assert.Equal(HttpStatusCode.NoContent, undoResp.StatusCode);

        var getPostAfterUndo = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{rootPost.Id}")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var postBodyAfterUndo = await getPostAfterUndo.Content.ReadFromJsonAsync<PostResponse>();
        Assert.NotNull(postBodyAfterUndo);
        Assert.False(postBodyAfterUndo.IsReposted);
    }
}