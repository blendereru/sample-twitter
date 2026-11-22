using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Realtime;

public class MessageDeliveryTests : IntegrationTestBase
{
    public MessageDeliveryTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SendMessage_RecipientConnected_ReceivesMessageReceivedWithExactCamelCasePayload()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        await using var bobHub = await ChatHubClient.ConnectAsync(Factory, bobCookie);

        // Act
        var response = await SendMessage(aliceCookie, conversation.Id, "Hello real-time!");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(sent);

        // Assert typed payload + raw camelCase JSON wire contract
        var received = await bobHub.WaitForMessageAsync();
        var raw = await bobHub.WaitForRawMessageAsync();

        Assert.Equal(sent.Id, received.Id);
        Assert.Equal(conversation.Id, received.ConversationId);
        Assert.Equal(alice.Id, received.SenderId);
        Assert.Equal("alice", received.SenderUsername);
        Assert.Equal("Hello real-time!", received.Text);

        Assert.True(raw.TryGetProperty("id", out var idProp) && idProp.GetInt64() == sent.Id);
        Assert.True(raw.TryGetProperty("conversationId", out var convProp) && convProp.GetInt64() == conversation.Id);
        Assert.True(raw.TryGetProperty("senderId", out var senderProp) && senderProp.GetInt64() == alice.Id);
        Assert.True(raw.TryGetProperty("senderUsername", out var userProp) && userProp.GetString() == "alice");
        Assert.True(raw.TryGetProperty("text", out var textProp) && textProp.GetString() == "Hello real-time!");
        Assert.True(raw.TryGetProperty("sentAt", out _));
    }

    [Fact]
    public async Task SendMessage_SenderHasSecondConnection_OtherTabAlsoReceivesIt()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        await using var aliceTab2 = await ChatHubClient.ConnectAsync(Factory, aliceCookie);

        // Act
        var response = await SendMessage(aliceCookie, conversation.Id, "Synced across tabs");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert
        var received = await aliceTab2.WaitForMessageAsync();
        Assert.Equal(conversation.Id, received.ConversationId);
        Assert.Equal("Synced across tabs", received.Text);
    }

    [Fact]
    public async Task SendMessage_UnrelatedUserConnected_ReceivesNothing()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var (carol, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!", username: "carol");

        var aliceBobConv = await SeedConversation(alice.Id, bob.Id);
        var aliceCarolConv = await SeedConversation(alice.Id, carol.Id);

        await using var carolHub = await ChatHubClient.ConnectAsync(Factory, carolCookie);

        // Act: 1) Alice messages Bob (Carol must NOT receive this)
        var privateResp = await SendMessage(aliceCookie, aliceBobConv.Id, "Secret for Bob");
        Assert.Equal(HttpStatusCode.Created, privateResp.StatusCode);

        // 2) Sentinel: Alice messages Carol directly — Carol's very first event must be the sentinel
        var sentinelResp = await SendMessage(aliceCookie, aliceCarolConv.Id, "Sentinel for Carol");
        Assert.Equal(HttpStatusCode.Created, sentinelResp.StatusCode);

        // Assert
        var firstForCarol = await carolHub.WaitForMessageAsync();
        Assert.Equal(aliceCarolConv.Id, firstForCarol.ConversationId);
        Assert.Equal("Sentinel for Carol", firstForCarol.Text);
    }

    [Fact]
    public async Task SendMessage_RecipientOffline_MessageStillPersistedAndAvailableViaAfterCursor()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);
        var initial = await SeedMessage(conversation.Id, alice.Id, "Before disconnect", DateTimeOffset.UtcNow.AddMinutes(-2));

        // Act: Bob is offline (no HubConnection)
        var offlineResp = await SendMessage(aliceCookie, conversation.Id, "Sent while Bob offline");
        Assert.Equal(HttpStatusCode.Created, offlineResp.StatusCode);

        // Bob comes back online and catches up via REST after cursor
        var catchUpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/conversations/{conversation.Id}/messages?after={initial.Id}")
        {
            Headers = { { "Cookie", bobCookie } }
        };
        var catchUpResponse = await Client.SendAsync(catchUpRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, catchUpResponse.StatusCode);
        var missed = await catchUpResponse.Content.ReadFromJsonAsync<List<MessageResponse>>();
        Assert.NotNull(missed);
        Assert.Single(missed);
        Assert.Equal("Sent while Bob offline", missed[0].Text);
    }

    [Fact]
    public async Task MarkAsRead_OtherParticipantReceivesConversationRead()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);
        await SeedMessage(conversation.Id, alice.Id, "Did you see this?");

        await using var aliceHub = await ChatHubClient.ConnectAsync(Factory, aliceCookie);

        // Act: Bob marks conversation as read
        var readRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/read")
        {
            Headers = { { "Cookie", bobCookie } }
        };
        var readResponse = await Client.SendAsync(readRequest);
        Assert.Equal(HttpStatusCode.NoContent, readResponse.StatusCode);

        // Assert: Alice receives ConversationRead receipt
        var receipt = await aliceHub.WaitForReadReceiptAsync();
        Assert.Equal(conversation.Id, receipt.ConversationId);
        Assert.Equal(bob.Id, receipt.ReaderId);
        Assert.True(receipt.ReadAt <= DateTimeOffset.UtcNow.AddSeconds(5));
    }

    private Task<HttpResponseMessage> SendMessage(string cookie, long conversationId, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversationId}/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest { Text = text }),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(request);
    }
}