using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.IntegrationTests.Conversations;

public class SendMessageTests : IntegrationTestBase
{
    public SendMessageTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SendMessage_ByParticipant_Returns201PersistsAndBumpsLastMessageAt()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var response = await SendMessageRequest(aliceCookie, conversation.Id, "  Hello Bob!  ");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal(conversation.Id, body.ConversationId);
        Assert.Equal(alice.Id, body.SenderId);
        Assert.Equal("alice", body.SenderUsername);
        Assert.Equal("Hello Bob!", body.Text);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var savedMsg = db.Messages.Single(m => m.Id == body.Id);
        var savedConv = db.Conversations.Single(c => c.Id == conversation.Id);
        Assert.Equal("Hello Bob!", savedMsg.Text);
        Assert.NotNull(savedConv.LastMessageAt);
    }

    [Fact]
    public async Task SendMessage_ByOutsider_Returns403AndDoesNotPersist()
    {
        // Arrange
        var alice = await SeedUser("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var (_, carolCookie) = await SeedAndSignIn("carol@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var response = await SendMessageRequest(carolCookie, conversation.Id, "Sneaky message");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.Forbidden);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Empty(db.Messages);
    }

    [Fact]
    public async Task SendMessage_NonExistentConversation_Returns404()
    {
        // Arrange
        var (_, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendMessageRequest(aliceCookie, 999999, "Hello?");

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public async Task SendMessage_BlankText_Returns400(string? text)
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var response = await SendMessageRequest(aliceCookie, conversation.Id, text);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_ExceedsMaxLength_Returns400ValidationProblemDetails()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        // Act
        var response = await SendMessageRequest(aliceCookie, conversation.Id, new string('x', Message.MaxTextLength + 1));

        // Assert
        await response.AssertValidationProblemDetails("Text");
    }

    [Fact]
    public async Task SendMessage_Unauthenticated_Returns401()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/conversations/1/messages", new SendMessageRequest { Text = "Hi" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendMessageRequest(string cookie, long conversationId, string? text)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversationId}/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest { Text = text }),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}