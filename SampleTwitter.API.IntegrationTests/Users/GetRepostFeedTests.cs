using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetRepostFeedTests : IntegrationTestBase
{
    public GetRepostFeedTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InvalidUserIdFormat_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/not-a-valid-id/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404ProblemDetails()
    {
        // Arrange
        const long nonExistentUserId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/users/{nonExistentUserId}/reposts");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns200_WithViewerFlagsFalse()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");
        var post = await SeedPost(author.Id, "Public post");

        await SeedRepost(reposter.Id, post.Id);
        await SeedBookmark(reposter.Id, post.Id);

        // Act (no auth cookie)
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("Public post", item.Text);
        Assert.Equal(author.Id, item.Author.Id);
        Assert.Equal(author.Email, item.Author.Email);
        Assert.Equal(1, item.RepostCount);
        Assert.False(item.IsBookmarked);
        Assert.False(item.IsReposted);
    }

    [Fact]
    public async Task AuthenticatedAsProfileOwner_ReturnsIsRepostedTrueAndAccurateIsBookmarked()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var now = DateTimeOffset.UtcNow;
        var post1 = await SeedPost(author.Id, "Post 1 (bookmarked)");
        var post2 = await SeedPost(author.Id, "Post 2 (not bookmarked)");

        await SeedRepost(owner.Id, post1.Id, createdAt: now.AddMinutes(-1));
        await SeedRepost(owner.Id, post2.Id, createdAt: now.AddMinutes(-5));
        await SeedBookmark(owner.Id, post1.Id);

        // Act
        var response = await SendGetRepostsRequest(owner.Id, ownerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        Assert.Equal(post1.Id, body[0].Id);
        Assert.True(body[0].IsReposted);
        Assert.True(body[0].IsBookmarked);

        Assert.Equal(post2.Id, body[1].Id);
        Assert.True(body[1].IsReposted);
        Assert.False(body[1].IsBookmarked);
    }

    [Fact]
    public async Task AuthenticatedAsDifferentViewer_ReflectsViewerSpecificBookmarkAndRepostFlags()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var profileUser = await SeedUser("profile@example.com", "Sup3rSecret1!");
        var (viewer, viewerCookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");

        var now = DateTimeOffset.UtcNow;
        var postA = await SeedPost(author.Id, "Reposted by profileUser only");
        var postB = await SeedPost(author.Id, "Reposted by both and bookmarked by viewer");

        await SeedRepost(profileUser.Id, postA.Id, createdAt: now.AddMinutes(-1));
        await SeedRepost(profileUser.Id, postB.Id, createdAt: now.AddMinutes(-2));

        // Viewer only reposted and bookmarked postB
        await SeedRepost(viewer.Id, postB.Id);
        await SeedBookmark(viewer.Id, postB.Id);
        // profileUser bookmarked postA (should NOT make IsBookmarked true for viewer)
        await SeedBookmark(profileUser.Id, postA.Id);

        // Act
        var response = await SendGetRepostsRequest(profileUser.Id, viewerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        Assert.Equal(postA.Id, body[0].Id);
        Assert.Equal(1, body[0].RepostCount);
        Assert.False(body[0].IsReposted);
        Assert.False(body[0].IsBookmarked);

        Assert.Equal(postB.Id, body[1].Id);
        Assert.Equal(2, body[1].RepostCount);
        Assert.True(body[1].IsReposted);
        Assert.True(body[1].IsBookmarked);
    }

    [Fact]
    public async Task UserWithNoReposts_Returns200WithEmptyList()
    {
        // Arrange
        var user = await SeedUser("noreposts@example.com", "Sup3rSecret1!");
        await SeedPost(user.Id, "Own unreposted post");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task OnlyReturnsTargetUsersReposts_NotOtherUsersReposts()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var userA = await SeedUser("usera@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");

        var postRepostedByA = await SeedPost(author.Id, "Post reposted by User A");
        var postRepostedByB = await SeedPost(author.Id, "Post reposted by User B");

        await SeedRepost(userA.Id, postRepostedByA.Id);
        await SeedRepost(userB.Id, postRepostedByB.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{userB.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(postRepostedByB.Id, item.Id);
        Assert.Equal("Post reposted by User B", item.Text);
    }

    [Fact]
    public async Task ReturnsRepostedPostsOrderedByMostRecentlyRepostedFirst_ThenByDescendingPostId()
    {
        // Arrange
        var author1 = await SeedUser("author1@example.com", "Sup3rSecret1!");
        var author2 = await SeedUser("author2@example.com", "Sup3rSecret1!");
        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        var now = DateTimeOffset.UtcNow;
        var sameRepostTime = now.AddMinutes(-10);

        // Post 1 and Post 2 reposted at the exact same timestamp -> Post 2 has higher ID so comes before Post 1
        var post1 = await SeedPost(author1.Id, "Post 1", createdAt: now.AddMinutes(-5));
        var post2 = await SeedPost(author1.Id, "Post 2", createdAt: now.AddMinutes(-20));
        // Post 3 is oldest by creation time, but reposted most recently -> comes first overall
        var post3 = await SeedPost(
            author2.Id,
            "Post 3",
            imageUrl: "https://example.com/img.png",
            createdAt: now.AddMinutes(-60),
            updatedAt: now.AddMinutes(-30));

        await SeedRepost(reposter.Id, post1.Id, createdAt: sameRepostTime);
        await SeedRepost(reposter.Id, post2.Id, createdAt: sameRepostTime);
        await SeedRepost(reposter.Id, post3.Id, createdAt: now.AddMinutes(-1));

        await SeedReply(author1.Id, post3.Id, "Reply on post 3");

        // Act
        var response = await SendGetRepostsRequest(reposter.Id, reposterCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Count);

        Assert.Equal(post3.Id, body[0].Id);
        Assert.Equal("Post 3", body[0].Text);
        Assert.Equal("https://example.com/img.png", body[0].ImageUrl);
        Assert.NotNull(body[0].UpdatedAt);
        Assert.Equal(author2.Id, body[0].Author.Id);
        Assert.Equal(author2.Email, body[0].Author.Email);
        Assert.Equal(1, body[0].RepostCount);
        Assert.Equal(1, body[0].ReplyCount);

        Assert.Equal(post2.Id, body[1].Id);
        Assert.Equal(post1.Id, body[2].Id);
    }

    [Fact]
    public async Task SoftDeletedPost_ExcludedFromRepostFeed()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");

        var keptPost = await SeedPost(author.Id, "Kept post");
        var deletedPost = await SeedPost(author.Id, "Deleted post", isDeleted: true);

        await SeedRepost(reposter.Id, keptPost.Id);
        await SeedRepost(reposter.Id, deletedPost.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(keptPost.Id, item.Id);
    }

    [Fact]
    public async Task UndoneRepost_ExcludedFromRepostFeed()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (reposter, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Post to repost and undo");
        await SeedRepost(reposter.Id, post.Id);

        var undoRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var undoResponse = await Client.SendAsync(undoRequest);
        undoResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task RepostedReply_ReturnsReplyIdAndParentAuthor()
    {
        // Arrange
        var parentAuthor = await SeedUser("parent@example.com", "Sup3rSecret1!");
        var replyAuthor = await SeedUser("replier@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(parentAuthor.Id, "Parent post");
        var reply = await SeedReply(replyAuthor.Id, parent.Id, "Reply to parent");

        await SeedRepost(reposter.Id, reply.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(parent.Id, item.ReplyId);
        Assert.NotNull(item.ReplyToAuthor);
        Assert.Equal(parentAuthor.Id, item.ReplyToAuthor.Id);
        Assert.Equal(parentAuthor.Email, item.ReplyToAuthor.Email);
    }

    [Fact]
    public async Task RepostedReply_WhenParentPostIsSoftDeleted_ReturnsReplyIdWithNullReplyToAuthor()
    {
        // Arrange
        var parentAuthor = await SeedUser("parent@example.com", "Sup3rSecret1!");
        var replyAuthor = await SeedUser("replier@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");

        var deletedParent = await SeedPost(parentAuthor.Id, "Deleted parent post", isDeleted: true);
        var reply = await SeedReply(replyAuthor.Id, deletedParent.Id, "Reply to deleted parent");

        await SeedRepost(reposter.Id, reply.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(deletedParent.Id, item.ReplyId);
        Assert.Null(item.ReplyToAuthor);
    }

    [Fact]
    public async Task SoftDeletedReplies_ExcludedFromReplyCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Reposted post");
        await SeedReply(author.Id, post.Id, "Live reply");
        await SeedPost(author.Id, "Deleted reply", replyId: post.Id, isDeleted: true);

        await SeedRepost(reposter.Id, post.Id);

        // Act
        var response = await Client.GetAsync($"/api/users/{reposter.Id}/reposts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(1, item.ReplyCount);
    }

    private Task<HttpResponseMessage> SendGetRepostsRequest(long userId, string cookie)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{userId}/reposts")
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}