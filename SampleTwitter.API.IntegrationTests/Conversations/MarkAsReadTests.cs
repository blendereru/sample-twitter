using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Conversations;

public class MarkAsReadTests : IntegrationTestBase
{
    public MarkAsReadTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task MarkAsRead_ByParticipant_Returns204AndClearsUnreadCount()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);
        await SeedMessage(conversation.Id, bob.Id, "Unread 1", DateTimeOffset.UtcNow.AddMinutes(-2));
        await SeedMessage(conversation.Id, bob.Id, "Unread 2", DateTimeOffset.UtcNow.AddMinutes(-1));

        // Act
        var readRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/read")
        {
            Headers = { { "Cookie", aliceCookie } }
        };
        var readResponse = await Client.SendAsync(readRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, readResponse.StatusCode);

        var inboxRequest = new HttpRequestMessage(HttpMethod.Get, "/api/conversations")
        {
            Headers = { { "Cookie", aliceCookie } }
        };
        var inboxResponse = await Client.SendAsync(inboxRequest);
        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<ConversationResponse>>();
        Assert.NotNull(inbox);
        Assert.Single(inbox);
        Assert.Equal(0, inbox[0].UnreadCount);
    }

    [Fact]
    public async Task MarkAsRead_ByOutsider_Returns403()
    {
        // Arrange
        var alice = await SeedUser("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var (_, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/read")
        {
            Headers = { { "Cookie", carolCookie } }
        };
        var response = await Client.SendAsync(request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkAsRead_NonExistentConversation_Returns404()
    {
        // Arrange
        var (_, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations/999999/read")
        {
            Headers = { { "Cookie", aliceCookie } }
        };
        var response = await Client.SendAsync(request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }
}