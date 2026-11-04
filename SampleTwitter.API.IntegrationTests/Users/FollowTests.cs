using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Users;

public class FollowTests : IntegrationTestBase
{
    public FollowTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ValidFollow_Returns200WithFollowResponse_AndPersistsInDatabase()
    {
        // Arrange
        var (follower, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await SendFollowRequest(followerCookie, target.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<FollowResponse>();
        Assert.NotNull(body);
        Assert.Equal(follower.Id, body.FollowerId);
        Assert.Equal(target.Id, body.FollowingId);
        Assert.True(body.CreatedAt >= before.AddSeconds(-1));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var followInDb = await db.Follows.SingleOrDefaultAsync(
            f => f.FollowerId == follower.Id && f.FollowingId == target.Id);
        Assert.NotNull(followInDb);
    }

    [Fact]
    public async Task SelfFollow_Returns400BadRequestWithProblemDetails()
    {
        // Arrange
        var (user, cookie) = await SeedAndSignIn("self@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendFollowRequest(cookie, user.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Equal(0, await db.Follows.CountAsync());
    }

    [Fact]
    public async Task AlreadyFollowing_SecondAttempt_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        var (follower, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");

        var firstResponse = await SendFollowRequest(followerCookie, target.Id);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act
        var secondResponse = await SendFollowRequest(followerCookie, target.Id);

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.Conflict);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Follows.CountAsync(f => f.FollowerId == follower.Id && f.FollowingId == target.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConcurrentFollows_SameFollowerAndTarget_HandlesPostgresUniqueConstraintRaceCondition()
    {
        // Arrange
        var (follower, followerCookie) = await SeedAndSignIn("follower_race@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target_race@example.com", "Sup3rSecret1!");

        using var barrier = Factory.FollowInsertBarrier.Arm(participants: 2, timeout: TimeSpan.FromSeconds(10));

        // Act
        var responses = await Task.WhenAll(
            SendFollowRequest(followerCookie, target.Id),
            SendFollowRequest(followerCookie, target.Id));

        // Assert
        Assert.Equal(2, barrier.ArrivedCount);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var count = await db.Follows.CountAsync(f => f.FollowerId == follower.Id && f.FollowingId == target.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Follow_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");

        // Act
        var response = await Client.PostAsync($"/api/users/{target.Id}/follow", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Follow_NonExistentTargetUser_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var (_, cookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendFollowRequest(cookie, targetUserId: 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfollow_ExistingFollow_Returns204NoContent_AndRemovesFromDatabase()
    {
        // Arrange
        var (follower, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");
        await SeedFollow(follower.Id, target.Id);

        // Act
        var response = await SendUnfollowRequest(followerCookie, target.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var followInDb = await db.Follows.SingleOrDefaultAsync(
            f => f.FollowerId == follower.Id && f.FollowingId == target.Id);
        Assert.Null(followInDb);
    }

    [Fact]
    public async Task Unfollow_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUnfollowRequest(null, target.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unfollow_NotFollowing_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var (_, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUnfollowRequest(followerCookie, target.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfollow_NonExistentUser_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var (_, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendUnfollowRequest(followerCookie, targetUserId: 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unfollow_TwiceInSuccession_SecondAttemptReturns404WithProblemDetails()
    {
        // Arrange
        var (follower, followerCookie) = await SeedAndSignIn("follower@example.com", "Sup3rSecret1!");
        var target = await SeedUser("target@example.com", "Sup3rSecret1!");
        await SeedFollow(follower.Id, target.Id);

        var firstResponse = await SendUnfollowRequest(followerCookie, target.Id);
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);

        // Act
        var secondResponse = await SendUnfollowRequest(followerCookie, target.Id);

        // Assert
        await secondResponse.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_ReturnsAccurateFollowersCountFollowingCountAndViewerSpecificIsFollowing()
    {
        // Arrange
        var (viewer, viewerCookie) = await SeedAndSignIn("viewer@example.com", "Sup3rSecret1!");
        var (otherUser, otherCookie) = await SeedAndSignIn("other@example.com", "Sup3rSecret1!");
        var targetUser = await SeedUser("target@example.com", "Sup3rSecret1!");

        // targetUser has 2 followers (viewer and otherUser) and follows 1 user (otherUser)
        await SeedFollow(viewer.Id, targetUser.Id);
        await SeedFollow(otherUser.Id, targetUser.Id);
        await SeedFollow(targetUser.Id, otherUser.Id);

        // 1. As viewer (who follows targetUser) -> IsFollowing = true
        var viewerGetResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/users/{targetUser.Id}")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        Assert.Equal(HttpStatusCode.OK, viewerGetResp.StatusCode);
        var viewerBody = await viewerGetResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(viewerBody);
        Assert.Equal(2, viewerBody.FollowersCount);
        Assert.Equal(1, viewerBody.FollowingCount);
        Assert.True(viewerBody.IsFollowing);

        // 2. As anonymous -> IsFollowing = false, counts still returned
        var anonGetResp = await Client.GetAsync($"/api/users/{targetUser.Id}");
        var anonBody = await anonGetResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(anonBody);
        Assert.Equal(2, anonBody.FollowersCount);
        Assert.Equal(1, anonBody.FollowingCount);
        Assert.False(anonBody.IsFollowing);

        // 3. Viewer unfollows targetUser -> FollowersCount becomes 1, IsFollowing becomes false
        var unfollowResp = await SendUnfollowRequest(viewerCookie, targetUser.Id);
        Assert.Equal(HttpStatusCode.NoContent, unfollowResp.StatusCode);

        var afterUnfollowResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/users/{targetUser.Id}")
        {
            Headers = { { "Cookie", viewerCookie } }
        });
        var afterUnfollowBody = await afterUnfollowResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(afterUnfollowBody);
        Assert.Equal(1, afterUnfollowBody.FollowersCount);
        Assert.Equal(1, afterUnfollowBody.FollowingCount);
        Assert.False(afterUnfollowBody.IsFollowing);

        // 4. OtherUser still sees IsFollowing = true
        var otherGetResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/users/{targetUser.Id}")
        {
            Headers = { { "Cookie", otherCookie } }
        });
        var otherBody = await otherGetResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(otherBody);
        Assert.True(otherBody.IsFollowing);
    }

    private Task<HttpResponseMessage> SendFollowRequest(string cookie, long targetUserId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{targetUserId}/follow")
        {
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }

    private Task<HttpResponseMessage> SendUnfollowRequest(string? cookie, long targetUserId)
    {
        var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{targetUserId}/follow");
        if (cookie is not null)
        {
            message.Headers.Add("Cookie", cookie);
        }
        return Client.SendAsync(message);
    }
}