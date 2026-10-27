using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class BookmarkTests : IntegrationTestBase
{
    public BookmarkTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidBookmark_Returns200WithBookmarkResponse()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "original post");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BookmarkResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.Equal(bookmarker.Id, body.UserId);
        Assert.True(body.CreatedAt >= before.AddSeconds(-1));
    }

    [Fact]
    public async Task ValidBookmark_PersistsInDatabase()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to bookmark");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendBookmarkRequest(bookmarkerCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.PostId == post.Id && b.UserId == bookmarker.Id);

        Assert.NotNull(bookmark);
        Assert.Equal(post.Id, bookmark.PostId);
        Assert.Equal(bookmarker.Id, bookmark.UserId);
    }

    [Fact]
    public async Task ValidBookmark_AuthorBookmarkingOwnPost_Succeeds()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "author's own post");

        // Act
        var response = await SendBookmarkRequest(authorCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BookmarkResponse>();
        Assert.NotNull(body);
        Assert.Equal(post.Id, body.PostId);
        Assert.Equal(author.Id, body.UserId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.PostId == post.Id && b.UserId == author.Id);
        Assert.NotNull(bookmark);
    }

    [Fact]
    public async Task AlreadyBookmarked_SecondAttempt_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to bookmark twice");

        var (_, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");

        var firstResponse = await SendBookmarkRequest(bookmarkerCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act
        var secondResponse = await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AlreadyBookmarked_DoesNotDuplicateDatabaseRecord()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "duplicate test post");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");

        await SendBookmarkRequest(bookmarkerCookie, post.Id);
        await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Bookmarks.CountAsync(b => b.PostId == post.Id && b.UserId == bookmarker.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConcurrentBookmarks_SameUserAndPost_HandlesPostgresUniqueConstraintRaceCondition()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_race@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "race test post");

        var (_, bookmarkerCookie) = await SeedAndSignIn("bookmarker_race@example.com", "Sup3rSecret1!");

        using var barrier = Factory.BookmarkInsertBarrier.Arm(participants: 2, timeout: TimeSpan.FromSeconds(10));

        // Act
        var responses = await Task.WhenAll(
            SendBookmarkRequest(bookmarkerCookie, post.Id),
            SendBookmarkRequest(bookmarkerCookie, post.Id));

        // Assert
        Assert.Equal(2, barrier.ArrivedCount);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Bookmarks.CountAsync(b => b.PostId == post.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DifferentUsers_CanBookmarkSamePost_BothPersist()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "viral post");

        var (user2, cookie2) = await SeedAndSignIn("user2@example.com", "Sup3rSecret1!");
        var (user3, cookie3) = await SeedAndSignIn("user3@example.com", "Sup3rSecret1!");

        // Act
        var resp2 = await SendBookmarkRequest(cookie2, post.Id);
        var resp3 = await SendBookmarkRequest(cookie3, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var bookmarks = await db.Bookmarks.Where(b => b.PostId == post.Id).ToListAsync();
        Assert.Equal(2, bookmarks.Count);
        Assert.Contains(bookmarks, b => b.UserId == user2.Id);
        Assert.Contains(bookmarks, b => b.UserId == user3.Id);
    }

    [Fact]
    public async Task SameUser_CanBookmarkMultipleDifferentPosts()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var postA = await SeedPost(userId: author.Id, text: "post A");
        var postB = await SeedPost(userId: author.Id, text: "post B");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");

        // Act
        var respA = await SendBookmarkRequest(bookmarkerCookie, postA.Id);
        var respB = await SendBookmarkRequest(bookmarkerCookie, postB.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Bookmarks.CountAsync(b => b.UserId == bookmarker.Id);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "public post");

        // Act
        var response = await Client.PostAsync($"/api/posts/{post.Id}/bookmark", null);

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
        await Client.PostAsync($"/api/posts/{post.Id}/bookmark", null);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Bookmarks.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task NonExistentPostId_Returns404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendBookmarkRequest(cookie, postId: 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedPost_Returns404()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post soon deleted");

        var deleteResponse = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var (_, bookmarkerCookie) = await SeedAndSignIn("bookmarker@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/posts/not-a-number/bookmark")
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
    public async Task UndoBookmark_ExistingBookmark_Returns204AndDeletesBookmark()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to undo bookmark on");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker_undo@example.com", "Sup3rSecret1!");
        var bookmarkResponse = await SendBookmarkRequest(bookmarkerCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, bookmarkResponse.StatusCode);

        // Act
        var undoResponse = await SendUndoBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, undoResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var bookmarkInDb = await db.Bookmarks.SingleOrDefaultAsync(b => b.PostId == post.Id && b.UserId == bookmarker.Id);
        Assert.Null(bookmarkInDb);
    }

    [Fact]
    public async Task UndoBookmark_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth undo");

        // Act
        var response = await SendUndoBookmarkRequest(null, post.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UndoBookmark_Unauthenticated_DoesNotMutateDatabase()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_unauth_db@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for unauth db test");

        var (bookmarker, bookmarkerCookie) = await SeedAndSignIn("bookmarker_unauth_db@example.com", "Sup3rSecret1!");
        await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Act
        var response = await SendUndoBookmarkRequest(null, post.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Assert
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var bookmark = await db.Bookmarks.SingleOrDefaultAsync(b => b.PostId == post.Id && b.UserId == bookmarker.Id);
        Assert.NotNull(bookmark);
    }

    [Fact]
    public async Task UndoBookmark_BookmarkDoesNotExist_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_notbookmarked@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post never bookmarked");

        var (_, userCookie) = await SeedAndSignIn("user_neverbookmarked@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoBookmarkRequest(userCookie, post.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoBookmark_NonExistentPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (_, userCookie) = await SeedAndSignIn("user_nonexistent@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUndoBookmarkRequest(userCookie, 99999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoBookmark_SoftDeletedPost_Returns404WithProblemDetails()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author_soft_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post to be soft deleted");

        var (_, bookmarkerCookie) = await SeedAndSignIn("bookmarker_soft_undo@example.com", "Sup3rSecret1!");
        await SendBookmarkRequest(bookmarkerCookie, post.Id);

        // Soft delete original post
        var deleteResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var undoResponse = await SendUndoBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert
        await undoResponse.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UndoBookmark_InvalidPostIdFormat_Returns400Or404()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("user_invalid_undo@example.com", "Sup3rSecret1!");

        // Act
        var message = new HttpRequestMessage(HttpMethod.Delete, "/api/posts/not-a-number/bookmark")
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
    public async Task UndoBookmark_TwiceInSuccession_SecondAttemptReturns404WithProblemDetails()
    {
        // Arrange
        var (author, _) = await SeedAndSignIn("author_double_undo@example.com", "Sup3rSecret1!");
        var post = await SeedPost(userId: author.Id, text: "post for double undo");

        var (_, bookmarkerCookie) = await SeedAndSignIn("bookmarker_double_undo@example.com", "Sup3rSecret1!");
        var bookmarkResp = await SendBookmarkRequest(bookmarkerCookie, post.Id);
        Assert.Equal(HttpStatusCode.OK, bookmarkResp.StatusCode);

        // Act 1 — First undo succeeds
        var firstUndo = await SendUndoBookmarkRequest(bookmarkerCookie, post.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstUndo.StatusCode);

        // Act 2 — Second undo immediately following
        var secondUndo = await SendUndoBookmarkRequest(bookmarkerCookie, post.Id);

        // Assert 2 — Returns 404 ProblemDetails
        await secondUndo.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> SendBookmarkRequest(string cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{postId}/bookmark")
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private Task<HttpResponseMessage> SendUndoBookmarkRequest(string? cookie, long postId)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}/bookmark");
        if (cookie is not null)
        {
            message.Headers.Add("Cookie", cookie);
        }
        return Client.SendAsync(message);
    }
}