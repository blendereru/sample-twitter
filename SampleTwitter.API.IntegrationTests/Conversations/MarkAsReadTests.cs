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

    [Fact]
    public async Task MarkAsRead_PersistsPerMessageReadAtWithoutOverwritingPreviouslyReadMessages()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);
        var msg1 = await SeedMessage(conversation.Id, alice.Id, "First message", DateTimeOffset.UtcNow.AddMinutes(-5));

        // Act 1: Bob reads first message
        var firstReadResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/read")
        {
            Headers = { { "Cookie", bobCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, firstReadResp.StatusCode);

        var afterFirstRead = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversation.Id}/messages")
        {
            Headers = { { "Cookie", aliceCookie } }
        });
        var firstPage = await afterFirstRead.Content.ReadFromJsonAsync<List<MessageResponse>>();
        Assert.NotNull(firstPage);
        var firstMsgReadAt = firstPage.Single(m => m.Id == msg1.Id).ReadAt;
        Assert.NotNull(firstMsgReadAt);

        // Act 2: Alice sends a second message and Bob reads it later
        var msg2 = await SeedMessage(conversation.Id, alice.Id, "Second message", DateTimeOffset.UtcNow);
        await Task.Delay(50);

        var secondReadResp = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/read")
        {
            Headers = { { "Cookie", bobCookie } }
        });
        Assert.Equal(HttpStatusCode.NoContent, secondReadResp.StatusCode);

        // Assert: msg1 preserved its original ReadAt while msg2 got a newer ReadAt
        var afterSecondRead = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversation.Id}/messages")
        {
            Headers = { { "Cookie", aliceCookie } }
        });
        var secondPage = await afterSecondRead.Content.ReadFromJsonAsync<List<MessageResponse>>();
        Assert.NotNull(secondPage);

        var updatedMsg1 = secondPage.Single(m => m.Id == msg1.Id);
        var updatedMsg2 = secondPage.Single(m => m.Id == msg2.Id);

        Assert.Equal(firstMsgReadAt.Value.ToUnixTimeMilliseconds(), updatedMsg1.ReadAt!.Value.ToUnixTimeMilliseconds());
        Assert.NotNull(updatedMsg2.ReadAt);
        Assert.True(updatedMsg2.ReadAt.Value > updatedMsg1.ReadAt.Value);
    }
}