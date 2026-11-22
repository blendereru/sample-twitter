using System.Net;
using Microsoft.AspNetCore.SignalR;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Realtime;

public class ChatHubTests : IntegrationTestBase
{
    public ChatHubTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Connect_WithoutCookie_Fails401()
    {
        // Arrange
        await using var connection = ChatHubClient.BuildConnection(Factory, cookie: null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task Typing_ByParticipant_RelayedOnlyToOtherParticipant()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");
        var (carol, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!", username: "carol");

        var aliceBobConv = await SeedConversation(alice.Id, bob.Id);
        var aliceCarolConv = await SeedConversation(alice.Id, carol.Id);

        await using var aliceHub = await ChatHubClient.ConnectAsync(Factory, aliceCookie);
        await using var bobHub = await ChatHubClient.ConnectAsync(Factory, bobCookie);
        await using var carolHub = await ChatHubClient.ConnectAsync(Factory, carolCookie);

        // Act: Alice types in Alice<->Bob conversation
        await aliceHub.SendTypingAsync(aliceBobConv.Id);

        // Assert: Bob receives TypingStarted
        var bobTyping = await bobHub.WaitForTypingAsync();
        Assert.Equal(aliceBobConv.Id, bobTyping.ConversationId);
        Assert.Equal(alice.Id, bobTyping.UserId);

        // Sentinel for Carol: Alice now types in Alice<->Carol; Carol's FIRST typing event must be for aliceCarolConv
        await aliceHub.SendTypingAsync(aliceCarolConv.Id);
        var carolTyping = await carolHub.WaitForTypingAsync();
        Assert.Equal(aliceCarolConv.Id, carolTyping.ConversationId);
        Assert.Equal(alice.Id, carolTyping.UserId);
    }

    [Fact]
    public async Task Typing_ByNonParticipant_ThrowsHubException()
    {
        // Arrange
        var alice = await SeedUser("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var (_, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        await using var carolHub = await ChatHubClient.ConnectAsync(Factory, carolCookie);

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() => carolHub.SendTypingAsync(conversation.Id));
    }
}
