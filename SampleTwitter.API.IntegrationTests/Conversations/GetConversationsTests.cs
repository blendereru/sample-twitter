using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Conversations;

public class GetConversationsTests : IntegrationTestBase
{
    public GetConversationsTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetConversations_ReturnsOnlyMyConversationsOrderedByLatestActivityWithUnreadCounts()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var carol = await SeedUser("carol@example.com", "Sup3rSecret1!", username: "carol");
        var dave = await SeedUser("dave@example.com", "Sup3rSecret1!", username: "dave");

        var now = DateTimeOffset.UtcNow;

        // Conversation 1: Alice <-> Bob (older)
        var convBob = await SeedConversation(alice.Id, bob.Id, now.AddMinutes(-30));
        await SeedMessage(convBob.Id, alice.Id, "Hi Bob", now.AddMinutes(-20));
        await SeedMessage(convBob.Id, bob.Id, "Hey Alice 1", now.AddMinutes(-15));
        await SeedMessage(convBob.Id, bob.Id, "Hey Alice 2", now.AddMinutes(-10));

        // Conversation 2: Alice <-> Carol (newer, only Alice sent a message -> unread should be 0 for Alice)
        var convCarol = await SeedConversation(alice.Id, carol.Id, now.AddMinutes(-25));
        await SeedMessage(convCarol.Id, alice.Id, "Ping Carol", now.AddMinutes(-5));

        // Conversation 3: Bob <-> Dave (unrelated to Alice)
        var convUnrelated = await SeedConversation(bob.Id, dave.Id, now.AddMinutes(-2));
        await SeedMessage(convUnrelated.Id, bob.Id, "Private", now.AddMinutes(-1));

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/conversations")
        {
            Headers = { { "Cookie", aliceCookie } }
        };
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ConversationResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        // Most recent first: Carol, then Bob
        Assert.Equal(convCarol.Id, body[0].Id);
        Assert.Equal("carol", body[0].OtherParticipant.Username);
        Assert.Equal("Ping Carol", body[0].LastMessage?.Text);
        Assert.Equal(0, body[0].UnreadCount);

        Assert.Equal(convBob.Id, body[1].Id);
        Assert.Equal("bob", body[1].OtherParticipant.Username);
        Assert.Equal("Hey Alice 2", body[1].LastMessage?.Text);
        Assert.Equal(2, body[1].UnreadCount);
    }

    [Fact]
    public async Task GetConversations_Unauthenticated_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/api/conversations");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}