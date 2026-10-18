using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Posts;

public class GetRepliesTests : IntegrationTestBase
{
    public GetRepliesTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InvalidPostIdFormat_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/posts/not-a-valid-id/replies");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentPostId_Returns404ProblemDetails()
    {
        // Arrange
        const long nonExistentPostId = 999999;

        // Act
        var response = await Client.GetAsync($"/api/posts/{nonExistentPostId}/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SoftDeletedPost_Returns404ProblemDetails()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var replier = await SeedUser("replier@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Post that will be deleted");
        await SeedReply(replier.Id, post.Id, "Reply to soon-to-be-deleted post");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns200()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(author.Id, "Public root post");
        await SeedReply(author.Id, post.Id, "Public reply");

        // Act (no auth cookie provided)
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Single(body);
    }

    [Fact]
    public async Task AuthenticatedViewer_Returns200()
    {
        // Arrange
        var (_, viewerCookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(author.Id, "Root post");
        await SeedReply(author.Id, post.Id, "Reply");

        var message = new HttpRequestMessage(HttpMethod.Get, $"/api/posts/{post.Id}/replies")
        {
            Headers = { { "Cookie", viewerCookie } }
        };

        // Act
        var response = await Client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Single(body);
    }

    [Fact]
    public async Task PostExistsWithNoReplies_Returns200WithEmptyList()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var post = await SeedPost(author.Id, "Post without replies");

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task RepliesToOtherPostsAndRootPosts_ExcludedFromResult()
    {
        // Arrange
        var user = await SeedUser("user@example.com", "Sup3rSecret1!");
        var targetPost = await SeedPost(user.Id, "Target post");
        var otherPost = await SeedPost(user.Id, "Other post");
        await SeedPost(user.Id, "Unrelated root post");

        await SeedReply(user.Id, otherPost.Id, "Reply to other post");
        var targetReply = await SeedReply(user.Id, targetPost.Id, "Reply to target post");

        // Act
        var response = await Client.GetAsync($"/api/posts/{targetPost.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(targetReply.Id, item.Id);
        Assert.Equal(targetPost.Id, item.ReplyId);
    }

    [Fact]
    public async Task MultipleAuthorsAndSelfReplies_AllDirectRepliesIncluded()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");
        var userC = await SeedUser("userc@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Discussion post");

        var now = DateTimeOffset.UtcNow;
        var replyFromAuthor = await SeedPost(author.Id, "Self reply", replyId: post.Id, createdAt: now.AddMinutes(-3));
        var replyFromB = await SeedPost(userB.Id, "Reply from B", replyId: post.Id, createdAt: now.AddMinutes(-2));
        var replyFromC = await SeedPost(userC.Id, "Reply from C", replyId: post.Id, createdAt: now.AddMinutes(-1));

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Count);

        Assert.Equal(replyFromC.Id, body[0].Id);
        Assert.Equal(userC.Id, body[0].Author.Id);
        Assert.Equal(userC.Email, body[0].Author.Email);

        Assert.Equal(replyFromB.Id, body[1].Id);
        Assert.Equal(userB.Id, body[1].Author.Id);
        Assert.Equal(userB.Email, body[1].Author.Email);

        Assert.Equal(replyFromAuthor.Id, body[2].Id);
        Assert.Equal(author.Id, body[2].Author.Id);
        Assert.Equal(author.Email, body[2].Author.Email);
    }

    [Fact]
    public async Task NestedReplies_OnlyDirectRepliesReturnedForEachPost()
    {
        // Arrange
        var userA = await SeedUser("usera@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");

        var rootPost = await SeedPost(userA.Id, "Root post");
        var directReply = await SeedReply(userB.Id, rootPost.Id, "Direct reply to root");
        var nestedReply = await SeedReply(userA.Id, directReply.Id, "Nested reply to direct reply");

        // Act 1: Get replies for rootPost
        var rootRepliesResponse = await Client.GetAsync($"/api/posts/{rootPost.Id}/replies");

        // Assert 1: Only directReply is returned
        Assert.Equal(HttpStatusCode.OK, rootRepliesResponse.StatusCode);
        var rootReplies = await rootRepliesResponse.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(rootReplies);
        var singleRootReply = Assert.Single(rootReplies);
        Assert.Equal(directReply.Id, singleRootReply.Id);
        Assert.Equal(rootPost.Id, singleRootReply.ReplyId);
        Assert.Equal(1, singleRootReply.ReplyCount);

        // Act 2: Get replies for directReply
        var nestedRepliesResponse = await Client.GetAsync($"/api/posts/{directReply.Id}/replies");

        // Assert 2: nestedReply is returned when querying directReply
        Assert.Equal(HttpStatusCode.OK, nestedRepliesResponse.StatusCode);
        var nestedReplies = await nestedRepliesResponse.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(nestedReplies);
        var singleNestedReply = Assert.Single(nestedReplies);
        Assert.Equal(nestedReply.Id, singleNestedReply.Id);
        Assert.Equal(directReply.Id, singleNestedReply.ReplyId);
        Assert.Equal(0, singleNestedReply.ReplyCount);
    }

    [Fact]
    public async Task RepostsOfPost_ExcludedFromReplies()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var reposter = await SeedUser("reposter@example.com", "Sup3rSecret1!");

        var post = await SeedPost(author.Id, "Root post");
        await SeedRepost(reposter.Id, post.Id);

        // Act
        var response = await Client.GetAsync($"/api/posts/{post.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Replies_OrderedByDescendingCreatedAt()
    {
        // Arrange
        var user = await SeedUser("order@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var now = DateTimeOffset.UtcNow;
        var oldest = await SeedPost(user.Id, "Oldest reply", replyId: parent.Id, createdAt: now.AddHours(-2));
        var middle = await SeedPost(user.Id, "Middle reply", replyId: parent.Id, createdAt: now.AddHours(-1));
        var newest = await SeedPost(user.Id, "Newest reply", replyId: parent.Id, createdAt: now);

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Count);
        Assert.Equal(newest.Id, body[0].Id);
        Assert.Equal(middle.Id, body[1].Id);
        Assert.Equal(oldest.Id, body[2].Id);
    }

    [Fact]
    public async Task SameCreatedAt_OrdersByDescendingId()
    {
        // Arrange
        var user = await SeedUser("sametime@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var fixedTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var first = await SeedPost(user.Id, "First created", replyId: parent.Id, createdAt: fixedTime);
        var second = await SeedPost(user.Id, "Second created", replyId: parent.Id, createdAt: fixedTime);

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.True(second.Id > first.Id);
        Assert.Equal(second.Id, body[0].Id);
        Assert.Equal(first.Id, body[1].Id);
    }

    [Fact]
    public async Task EditedReply_PreservesOriginalCreatedAtOrder()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("editor@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var now = DateTimeOffset.UtcNow;
        var olderReply = await SeedPost(user.Id, "Older reply", replyId: parent.Id, createdAt: now.AddHours(-2));
        var newerReply = await SeedPost(user.Id, "Newer reply", replyId: parent.Id, createdAt: now.AddHours(-1));

        var editRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{olderReply.Id}")
        {
            Headers = { { "Cookie", cookie } },
            Content = JsonContent.Create(new EditPostRequest { Text = "Older reply edited later" })
        };
        var editResponse = await Client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.Equal(newerReply.Id, body[0].Id);
        Assert.Null(body[0].UpdatedAt);
        Assert.Equal(olderReply.Id, body[1].Id);
        Assert.Equal("Older reply edited later", body[1].Text);
        Assert.NotNull(body[1].UpdatedAt);
    }

    [Fact]
    public async Task MapsAllReplyFields_Accurately()
    {
        // Arrange
        var postAuthor = await SeedUser("postauthor@example.com", "Sup3rSecret1!");
        var (replyAuthor, replyAuthorCookie) = await SeedAndSignIn("replyauthor@example.com", "Sup3rSecret1!");

        var parentPost = await SeedPost(postAuthor.Id, "Parent post");

        var replyTime = DateTimeOffset.UtcNow.AddHours(-1);
        var reply = await SeedPost(
            replyAuthor.Id,
            text: "Initial reply text",
            imageUrl: "https://example.com/initial.png",
            replyId: parentPost.Id,
            createdAt: replyTime);

        const string updatedText = "Edited reply with Unicode 🚀🎉 and special chars <>&\"'!";
        const string updatedImage = "https://example.com/updated-reply.png";
        var editRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/posts/{reply.Id}")
        {
            Headers = { { "Cookie", replyAuthorCookie } },
            Content = JsonContent.Create(new EditPostRequest { Text = updatedText, ImageUrl = updatedImage })
        };
        var editResponse = await Client.SendAsync(editRequest);
        editResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{parentPost.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);

        var item = Assert.Single(body);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(updatedText, item.Text);
        Assert.Equal(updatedImage, item.ImageUrl);
        Assert.Equal(replyTime.ToUnixTimeSeconds(), item.CreatedAt.ToUnixTimeSeconds());
        Assert.NotNull(item.UpdatedAt);
        Assert.Equal(replyAuthor.Id, item.Author.Id);
        Assert.Equal(replyAuthor.Email, item.Author.Email);
        Assert.Equal(parentPost.Id, item.ReplyId);
        Assert.Equal(0, item.RepostCount);
        Assert.Equal(0, item.ReplyCount);
    }

    [Fact]
    public async Task TextOnlyAndImageOnlyReplies_MappedCorrectly()
    {
        // Arrange
        var user = await SeedUser("media@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(user.Id, "Parent post");

        var now = DateTimeOffset.UtcNow;
        var textOnly = await SeedPost(
            user.Id,
            text: "Only text here",
            imageUrl: null,
            replyId: parent.Id,
            createdAt: now.AddMinutes(-2));
        var imageOnly = await SeedPost(
            user.Id,
            text: null,
            imageUrl: "https://example.com/only-image.png",
            replyId: parent.Id,
            createdAt: now.AddMinutes(-1));

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        Assert.Equal(imageOnly.Id, body[0].Id);
        Assert.Null(body[0].Text);
        Assert.Equal("https://example.com/only-image.png", body[0].ImageUrl);

        Assert.Equal(textOnly.Id, body[1].Id);
        Assert.Equal("Only text here", body[1].Text);
        Assert.Null(body[1].ImageUrl);
    }

    [Fact]
    public async Task ReplyCounters_AccuratelyReflectRepostsAndDirectNestedReplies()
    {
        // Arrange
        var userA = await SeedUser("usera@example.com", "Sup3rSecret1!");
        var userB = await SeedUser("userb@example.com", "Sup3rSecret1!");
        var userC = await SeedUser("userc@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(userA.Id, "Root post");
        var reply = await SeedReply(userB.Id, parent.Id, "Reply on root post");

        // 2 reposts on reply
        await SeedRepost(userA.Id, reply.Id);
        await SeedRepost(userC.Id, reply.Id);

        // 2 direct child replies on reply + 1 grandchild reply (grandchild must not count toward reply.ReplyCount)
        var child1 = await SeedReply(userA.Id, reply.Id, "Direct child 1");
        await SeedReply(userC.Id, reply.Id, "Direct child 2");
        await SeedReply(userB.Id, child1.Id, "Grandchild reply");

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);

        var item = Assert.Single(body);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(2, item.RepostCount);
        Assert.Equal(2, item.ReplyCount);
    }

    [Fact]
    public async Task UndoingRepostOnReply_DecrementsRepostCount()
    {
        // Arrange
        var author = await SeedUser("author@example.com", "Sup3rSecret1!");
        var (_, reposterCookie) = await SeedAndSignIn("reposter@example.com", "Sup3rSecret1!");

        var parent = await SeedPost(author.Id, "Parent post");
        var reply = await SeedReply(author.Id, parent.Id, "Reply to be reposted and undone");

        var repostRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{reply.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var repostResponse = await Client.SendAsync(repostRequest);
        repostResponse.EnsureSuccessStatusCode();

        var undoRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{reply.Id}/repost")
        {
            Headers = { { "Cookie", reposterCookie } }
        };
        var undoResponse = await Client.SendAsync(undoRequest);
        undoResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(0, item.RepostCount);
    }

    [Fact]
    public async Task SoftDeletedReply_ExcludedFromRepliesList()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(author.Id, "Parent post");
        var keptReply = await SeedReply(author.Id, parent.Id, "Kept reply");
        var deletedReply = await SeedReply(author.Id, parent.Id, "Reply to delete");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{deletedReply.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(keptReply.Id, item.Id);
    }

    [Fact]
    public async Task SoftDeletedNestedReply_ExcludedFromReplyCount()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var parent = await SeedPost(author.Id, "Parent post");
        var reply = await SeedReply(author.Id, parent.Id, "Reply");

        await SeedReply(author.Id, reply.Id, "Kept nested reply");
        var nestedToDelete = await SeedReply(author.Id, reply.Id, "Nested reply to delete");

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{nestedToDelete.Id}")
        {
            Headers = { { "Cookie", authorCookie } }
        };
        var deleteResponse = await Client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // Act
        var response = await Client.GetAsync($"/api/posts/{parent.Id}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(reply.Id, item.Id);
        Assert.Equal(1, item.ReplyCount);
    }

    [Fact]
    public async Task ReplyCreatedViaCreateEndpoint_ReturnedByGetReplies()
    {
        // Arrange
        var (author, authorCookie) = await SeedAndSignIn("author@example.com", "Sup3rSecret1!");
        var (replier, replierCookie) = await SeedAndSignIn("replier@example.com", "Sup3rSecret1!");

        var createParentMessage = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
        {
            Headers = { { "Cookie", authorCookie } },
            Content = JsonContent.Create(new CreatePostRequest { Text = "Parent created via API" })
        };
        var createParentResponse = await Client.SendAsync(createParentMessage);
        createParentResponse.EnsureSuccessStatusCode();
        var createdParent = await createParentResponse.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(createdParent);

        var createReplyMessage = new HttpRequestMessage(HttpMethod.Post, "/api/posts")
        {
            Headers = { { "Cookie", replierCookie } },
            Content = JsonContent.Create(new CreatePostRequest
            {
                Text = "Reply created via API",
                ImageUrl = "https://example.com/reply-pic.png",
                ReplyId = createdParent.PostId
            })
        };
        var createReplyResponse = await Client.SendAsync(createReplyMessage);
        createReplyResponse.EnsureSuccessStatusCode();
        var createdReply = await createReplyResponse.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(createdReply);

        // Act
        var response = await Client.GetAsync($"/api/posts/{createdParent.PostId}/replies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PostResponse>>();
        Assert.NotNull(body);
        var item = Assert.Single(body);
        Assert.Equal(createdReply.PostId, item.Id);
        Assert.Equal("Reply created via API", item.Text);
        Assert.Equal("https://example.com/reply-pic.png", item.ImageUrl);
        Assert.Equal(createdParent.PostId, item.ReplyId);
        Assert.Equal(replier.Id, item.Author.Id);
        Assert.Equal(replier.Email, item.Author.Email);
    }
}