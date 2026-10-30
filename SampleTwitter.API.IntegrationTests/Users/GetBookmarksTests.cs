using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetBookmarksTests : IntegrationTestBase
{
    private const string BookmarksUrl = "/api/users/bookmarks";

    public GetBookmarksTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Unauthenticated_Returns401Unauthorized()
    {
        // Act
        var response = await Client.GetAsync(BookmarksUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUser_OnlySeesOwnBookmarks_NotOtherUsersBookmarks()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (userA, _) = await SeedAndSignIn("usera@example.com", "Sup3rSecret1!");
        var (userB, userBCookie) = await SeedAndSignIn("userb@example.com", "Sup3rSecret1!");

        var postBookmarkedByA = await SeedPost(author.Id, "Post bookmarked by User A");
        var postBookmarkedByB = await SeedPost(author.Id, "Post bookmarked by User B");

        await SeedBookmark(userA.Id, postBookmarkedByA.Id);
        await SeedBookmark(userB.Id, postBookmarkedByB.Id);

        // Act
        var response = await SendGetBookmarksRequest(userBCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(postBookmarkedByB.Id, item.Id);
        Assert.Equal("Post bookmarked by User B", item.Text);
    }

    [Fact]
    public async Task AuthenticatedUser_WithNoBookmarks_Returns200WithEmptyList()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendGetBookmarksRequest(cookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task AuthenticatedUser_ReturnsBookmarkedPostsOrderedByMostRecentlyBookmarkedFirst()
    {
        // Arrange
        var author1 = await SeedUser("author1@example.com", "Sup3rSecret1!");
        var author2 = await SeedUser("author2@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var now = DateTimeOffset.UtcNow;
        // Post 1 is newer by creation time, but bookmarked earlier
        var post1 = await SeedPost(author1.Id, "Post 1", createdAt: now.AddMinutes(-5));
        // Post 2 is older by creation time, but bookmarked more recently
        var post2 = await SeedPost(author2.Id, "Post 2", imageUrl: "https://example.com/img.png", createdAt: now.AddMinutes(-30));
        // Unbookmarked post
        await SeedPost(author1.Id, "Unbookmarked post", createdAt: now);

        await SeedBookmark(owner.Id, post1.Id, createdAt: now.AddMinutes(-10));
        await SeedBookmark(owner.Id, post2.Id, createdAt: now.AddMinutes(-1));

        // Interactions on post2
        await SeedRepost(owner.Id, post2.Id);
        await SeedReply(author1.Id, post2.Id, "Reply on post 2");

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        // post2 was bookmarked more recently so it comes first
        Assert.Equal(post2.Id, body[0].Id);
        Assert.Equal("Post 2", body[0].Text);
        Assert.Equal("https://example.com/img.png", body[0].ImageUrl);
        Assert.Equal(author2.Id, body[0].Author.Id);
        Assert.Equal(author2.Email, body[0].Author.Email);
        Assert.Equal(1, body[0].RepostCount);
        Assert.Equal(1, body[0].ReplyCount);
        Assert.True(body[0].IsBookmarked);
        Assert.True(body[0].IsReposted);

        Assert.Equal(post1.Id, body[1].Id);
        Assert.Equal("Post 1", body[1].Text);
        Assert.Equal(author1.Id, body[1].Author.Id);
        Assert.Equal(0, body[1].RepostCount);
        Assert.Equal(0, body[1].ReplyCount);
        Assert.True(body[1].IsBookmarked);
        Assert.False(body[1].IsReposted);
    }

    [Fact]
    public async Task SoftDeletedPost_ExcludedFromBookmarksFeed()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var keptPost = await SeedPost(author.Id, "Kept post");
        var deletedPost = await SeedPost(author.Id, "Deleted post", isDeleted: true);

        await SeedBookmark(owner.Id, keptPost.Id);
        await SeedBookmark(owner.Id, deletedPost.Id);

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(keptPost.Id, item.Id);
    }

    [Fact]
    public async Task BookmarkedReply_ReturnsReplyIdAndParentAuthor()
    {
        // Arrange
        var parentAuthor = await SeedUser("parent@example.com", "Sup3rSecret1!");
        var replyAuthor = await SeedUser("replier@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(parentAuthor.Id, "Parent post");
        var reply = await SeedReply(replyAuthor.Id, parent.Id, "Reply to parent");

        await SeedBookmark(owner.Id, reply.Id);

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

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
    public async Task BookmarkedReply_WhenParentPostIsSoftDeleted_ReturnsReplyIdWithNullReplyToAuthor()
    {
        // Arrange
        var parentAuthor = await SeedUser("parent@example.com", "Sup3rSecret1!");
        var replyAuthor = await SeedUser("replier@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var deletedParent = await SeedPost(parentAuthor.Id, "Deleted parent post", isDeleted: true);
        var reply = await SeedReply(replyAuthor.Id, deletedParent.Id, "Reply to deleted parent");

        await SeedBookmark(owner.Id, reply.Id);

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

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
    public async Task RepostedByOtherUserOnly_IsRepostedFalseButCounted()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var otherUser = await SeedUser("other@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Post reposted by someone else");
        await SeedBookmark(owner.Id, post.Id);
        await SeedRepost(otherUser.Id, post.Id);

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(1, item.RepostCount);
        Assert.False(item.IsReposted);
    }

    [Fact]
    public async Task SoftDeletedReplies_ExcludedFromReplyCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (owner, ownerCookie) = await SeedAndSignIn("owner@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Bookmarked post");
        await SeedReply(author.Id, post.Id, "Live reply");
        await SeedPost(author.Id, "Deleted reply", replyId: post.Id, isDeleted: true);

        await SeedBookmark(owner.Id, post.Id);

        // Act
        var response = await SendGetBookmarksRequest(ownerCookie);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(1, item.ReplyCount);
    }

    private Task<HttpResponseMessage> SendGetBookmarksRequest(string cookie)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, BookmarksUrl)
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}