using System.Net;
using System.Net.Http.Json;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Conversations;

public class GetMessagesTests : IntegrationTestBase
{
    public GetMessagesTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetMessages_SupportsNewestFirstBeforeCursorAndAfterCursor()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        var seeded = new List<Message>();
        for (var i = 1; i <= 5; i++)
        {
            seeded.Add(await SeedMessage(conversation.Id, i % 2 == 0 ? bob.Id : alice.Id, $"msg-{i}", baseTime.AddMinutes(i)));
        }

        // Act 1: newest-first page (limit=2)
        var page1 = await GetMessages(aliceCookie, $"/api/conversations/{conversation.Id}/messages?limit=2");
        Assert.Equal(2, page1.Count);
        Assert.Equal("msg-5", page1[0].Text);
        Assert.Equal("msg-4", page1[1].Text);

        // Act 2: before cursor (older messages)
        var page2 = await GetMessages(aliceCookie, $"/api/conversations/{conversation.Id}/messages?before={page1[1].Id}&limit=2");
        Assert.Equal(2, page2.Count);
        Assert.Equal("msg-3", page2[0].Text);
        Assert.Equal("msg-2", page2[1].Text);

        // Act 3: after cursor (catch-up path)
        var catchUp = await GetMessages(aliceCookie, $"/api/conversations/{conversation.Id}/messages?after={seeded[2].Id}");
        Assert.Equal(2, catchUp.Count);
        Assert.Equal("msg-4", catchUp[0].Text);
        Assert.Equal("msg-5", catchUp[1].Text);
    }

    [Fact]
    public async Task GetMessages_LimitAbove50_IsCappedAt50()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-60);
        for (var i = 1; i <= 55; i++)
        {
            await SeedMessage(conversation.Id, alice.Id, $"msg-{i}", baseTime.AddSeconds(i));
        }

        // Act
        var messages = await GetMessages(aliceCookie, $"/api/conversations/{conversation.Id}/messages?limit=100");

        // Assert
        Assert.Equal(50, messages.Count);
    }

    [Fact]
    public async Task GetMessages_ByOutsider_Returns403()
    {
        // Arrange
        var alice = await SeedUser("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var (_, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversation.Id}/messages")
        {
            Headers = { { "Cookie", carolCookie } }
        };
        var response = await Client.SendAsync(request);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);
    }

    private async Task<List<MessageResponse>> GetMessages(string cookie, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Headers = { { "Cookie", cookie } }
        };
        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MessageResponse>>())!;
    }
}