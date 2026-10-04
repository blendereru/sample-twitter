using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Users;

public class GetProfileRepliesTests : IntegrationTestBase
{
    public GetProfileRepliesTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InvalidUserIdFormat_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/users/not-a-valid-id/replies");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentUserId_Returns404ProblemDetails()
    {
        // Arrange
        const long nonExistentUserId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/users/{nonExistentUserId}/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var parentPost = await SeedPost(author.Id, "Parent post");
        await SeedReply(author.Id, parentPost.Id, "Public reply");

        // Act (no auth cookie provided)
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task AuthenticatedViewer_Returns200()
    {
        // Arrange
        var (_, viewerCookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var parentPost = await SeedPost(author.Id, "Parent post");
        await SeedReply(author.Id, parentPost.Id, "Author reply");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{author.Id}/replies")
        {
            Headers = { { "Cookie", viewerCookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
    }

    [Fact]
    public async Task UserExistsWithNoReplies_Returns200WithEmptyList()
    {
        // Arrange
        var user = await SeedUser("lonely@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Author.Id);
        Assert.Equal(user.Email, body.Author.Email);
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
        var userReply = await SeedReply(user.Id, otherPost.Id, "User reply");

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
        var targetUser = await SeedUser("target@example.com", "Sup3rSecret1!");
        var otherUser = await SeedUser("other@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(otherUser.Id, "Root post");
        await SeedReply(otherUser.Id, parent.Id, "Other user reply");
        var targetReply = await SeedReply(targetUser.Id, parent.Id, "Target user reply");

        // Act
        var response = await Client.GetAsync($"/api/users/{targetUser.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(targetReply.Id, item.Id);
    }

    [Fact]
    public async Task RepliesToOwnPosts_ThreadContinuation_Included()
    {
        // Arrange
        var user = await SeedUser("threader@example.com", "Sup3rSecret1!");
        var rootPost = await SeedPost(user.Id, "First post of thread");
        var selfReply = await SeedReply(user.Id, rootPost.Id, "Second post of thread (reply)");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(selfReply.Id, item.Id);
        Assert.NotNull(item.ParentPost);
        Assert.Equal(rootPost.Id, item.ParentPost.Id);
    }

    [Fact]
    public async Task NestedReply_ReplyToAnotherReply_Included()
    {
        // Arrange
        var userA = await SeedUser("usera@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");

        var root = await SeedPost(userA.Id, "Root post");
        var replyB = await SeedReply(userB.Id, root.Id, "Reply from B");
        var nestedReplyA = await SeedReply(userA.Id, replyB.Id, "Nested reply from A to B's reply");

        // Act
        var response = await Client.GetAsync($"/api/users/{userA.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(nestedReplyA.Id, item.Id);
        Assert.NotNull(item.ParentPost);
        Assert.Equal(replyB.Id, item.ParentPost.Id);
        Assert.Equal("Reply from B", item.ParentPost.Text);
    }

    [Fact]
    public async Task RepostsByTheUser_ExcludedFromRepliesFeed()
    {
        // Arrange
        var user = await SeedUser("reposter@example.com", "Sup3rSecret1!");
        var other = await SeedUser("other@example.com", "Sup3rSecret1!");

        var post1 = await SeedPost(other.Id, "Post 1");
        var post2 = await SeedPost(other.Id, "Post 2");
        await SeedRepost(user.Id, post1.Id);
        await SeedRepost(user.Id, post2.Id);

        var reply = await SeedReply(user.Id, post1.Id, "Actual reply");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(reply.Id, item.Id);
    }

    [Fact]
    public async Task Replies_OrderedByDescendingCreatedAt()
    {
        // Arrange
        var user = await SeedUser("order@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var now = DateTimeOffset.UtcNow;
        var r1 = await SeedPost(user.Id, "Oldest reply", replyId: parent.Id, createdAt: now.AddHours(-2));
        var r2 = await SeedPost(user.Id, "Middle reply", replyId: parent.Id, createdAt: now.AddHours(-1));
        var r3 = await SeedPost(user.Id, "Newest reply", replyId: parent.Id, createdAt: now);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Items.Count);
        Assert.Equal(r3.Id, body.Items[0].Id);
        Assert.Equal(r2.Id, body.Items[1].Id);
        Assert.Equal(r1.Id, body.Items[2].Id);
    }

    [Fact]
    public async Task SameCreatedAt_OrdersByDescendingId()
    {
        // Arrange
        var user = await SeedUser("sametime@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var fixedTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var r1 = await SeedPost(user.Id, "First created", replyId: parent.Id, createdAt: fixedTime);
        var r2 = await SeedPost(user.Id, "Second created", replyId: parent.Id, createdAt: fixedTime);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        // Higher ID must be first due to ThenByDescending(p => p.Id)
        Assert.True(r2.Id > r1.Id);
        Assert.Equal(r2.Id, body.Items[0].Id);
        Assert.Equal(r1.Id, body.Items[1].Id);
    }

    [Fact]
    public async Task EditedReply_PreservesOriginalCreatedAtOrder()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("editor@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var now = DateTimeOffset.UtcNow;
        var r1 = await SeedPost(user.Id, "First reply", replyId: parent.Id, createdAt: now.AddHours(-2));
        var r2 = await SeedPost(user.Id, "Second reply", replyId: parent.Id, createdAt: now.AddHours(-1));

        // Edit r1 at current time (later than r2's CreatedAt)
        var editRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{r1.Id}")
        {
            Headers = { { "Cookie", cookie } },
            Content = JsonContent.Create(new EditPostRequest { Text = "First reply edited" })
        };
        var editResponse = await Client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);
        // Ordering should still be by CreatedAt DESC: r2 (T-1h) before r1 (T-2h)
        Assert.Equal(r2.Id, body.Items[0].Id);
        Assert.Equal(r1.Id, body.Items[1].Id);
        Assert.NotNull(body.Items[1].UpdatedAt);
    }

    [Fact]
    public async Task MapsAllReplyAndParentPostFields_Accurately()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("replyauthor@example.com", "Sup3rSecret1!");
        var parentAuthor = await SeedUser("parentauthor@example.com", "Sup3rSecret1!");

        var parentTime = DateTimeOffset.UtcNow.AddHours(-2);
        var parentPost = await SeedPost(
            parentAuthor.Id,
            text: "Parent tweet text with Unicode 🚀 and special chars <>&\"'!",
            imageUrl: "https://example.com/parent.png",
            createdAt: parentTime);

        var replyTime = DateTimeOffset.UtcNow.AddHours(-1);
        var reply = await SeedPost(
            author.Id,
            text: "Initial reply text",
            imageUrl: "https://example.com/reply.png",
            replyId: parentPost.Id,
            createdAt: replyTime);

        // Edit reply to populate UpdatedAt and test Unicode/emoji text preservation
        const string updatedText = "Edited reply text with emojis 🎉 and non-ASCII: Привет, мир!";
        var editRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{reply.Id}")
        {
            Headers = { { "Cookie", authorCookie } },
            Content = JsonContent.Create(new EditPostRequest { Text = updatedText, ImageUrl = "https://example.com/reply.png" })
        };
        var editResponse = await Client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);

        // Profile author mapping
        Assert.Equal(author.Id, body.Author.Id);
        Assert.Equal(author.Email, body.Author.Email);

        // Reply item mapping
        var item = Assert.Single(body.Items);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(updatedText, item.Text);
        Assert.Equal("https://example.com/reply.png", item.ImageUrl);
        Assert.Equal(replyTime.ToUnixTimeSeconds(), item.CreatedAt.ToUnixTimeSeconds());
        Assert.NotNull(item.UpdatedAt);

        // Parent post mapping
        Assert.NotNull(item.ParentPost);
        Assert.Equal(parentPost.Id, item.ParentPost.Id);
        Assert.Equal(parentPost.Text, item.ParentPost.Text);
        Assert.Equal(parentPost.ImageUrl, item.ParentPost.ImageUrl);
        Assert.Equal(parentTime.ToUnixTimeSeconds(), item.ParentPost.CreatedAt.ToUnixTimeSeconds());
        Assert.Null(item.ParentPost.UpdatedAt);
    }

    [Fact]
    public async Task ReplyAndParentCounters_AccuratelyReflectInteractions()
    {
        // Arrange
        var user = await SeedUser("counterauthor@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");
        var userC = await SeedUser("userc@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(userB.Id, "Parent post");
        // Parent interactions: 2 reposts + 3 replies (including user's reply)
        await SeedRepost(user.Id, parent.Id);
        await SeedRepost(userC.Id, parent.Id);
        await SeedReply(userC.Id, parent.Id, "Sibling reply 1");
        await SeedReply(userC.Id, parent.Id, "Sibling reply 2");

        var userReply = await SeedReply(user.Id, parent.Id, "User reply");

        // User reply interactions: 1 repost + 2 replies
        await SeedRepost(userB.Id, userReply.Id);
        await SeedReply(userB.Id, userReply.Id, "Reply to user reply 1");
        await SeedReply(userC.Id, userReply.Id, "Reply to user reply 2");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);

        Assert.Equal(1, item.RepostCount);
        Assert.Equal(2, item.ReplyCount);

        Assert.NotNull(item.ParentPost);
        Assert.Equal(2, item.ParentPost.RepostCount);
        Assert.Equal(3, item.ParentPost.ReplyCount);
    }

    [Fact]
    public async Task UndoingRepostOnReply_DecrementsRepostCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (_, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(author.Id, "Parent post");
        var reply = await SeedReply(author.Id, parent.Id, "Reply");

        // Repost via API
        var repostRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{reply.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var repostResponse = await Client.SendAsync(repostRequest);
        repostResponse.EnsureSuccessStatusCode();

        // Undo repost via API
        var undoRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{reply.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var undoResponse = await Client.SendAsync(undoRequest);
        undoResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(0, item.RepostCount);
    }

    [Fact]
    public async Task NestedRepliesToReply_OnlyDirectRepliesCounted()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent");

        var replyR = await SeedReply(user.Id, parent.Id, "Reply R");
        var childC = await SeedReply(user.Id, replyR.Id, "Child C");
        await SeedReply(user.Id, childC.Id, "Grandchild G");

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);

        var itemR = body.Items.Single(i => i.Id == replyR.Id);
        // Only direct child reply C should count for replyR, not grandchild G
        Assert.Equal(1, itemR.ReplyCount);
    }

    [Fact]
    public async Task SoftDeletedReply_ExcludedFromFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(author.Id, "Parent post");
        var reply = await SeedReply(author.Id, parent.Id, "Reply to be deleted");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{reply.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task SoftDeletedOwnParentPost_ReplyStillExistsInFeed()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("user@example.com", "Sup3rSecret1!");
        var post = await SeedPost(author.Id, "Own parent post");
        var reply = await SeedReply(author.Id, post.Id, "Reply to own post");

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
        var item = Assert.Single(body.Items);
        Assert.Equal(reply.Id, item.Id);
        Assert.Null(item.ParentPost);
    }

    [Fact]
    public async Task SoftDeletedOtherUserParentPost_ReplyStillExistsInFeed()
    {
        // Arrange
        var (userA, cookieA) = await SeedAndSignIn("usera@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");

        var parentPost = await SeedPost(userA.Id, "User A root post");
        var replyB = await SeedReply(userB.Id, parentPost.Id, "User B reply");

        // User A deletes their parent post
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{parentPost.Id}")
        {
            Headers = { { "Cookie", cookieA } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{userB.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal(replyB.Id, item.Id);
        Assert.Null(item.ParentPost);
    }

    [Fact]
    public async Task SoftDeletedChildReply_ExcludedFromReplyCount()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(author.Id, "Parent post");
        var replyR = await SeedReply(author.Id, parent.Id, "Reply R");

        var child1 = await SeedReply(author.Id, replyR.Id, "Active child reply");
        var child2 = await SeedReply(author.Id, replyR.Id, "Child reply to delete");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{child2.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/users/{author.Id}/replies");

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReplyFeedResponse>();
        Assert.NotNull(body);

        var itemR = body.Items.Single(i => i.Id == replyR.Id);
        Assert.Equal(1, itemR.ReplyCount);
    }

}