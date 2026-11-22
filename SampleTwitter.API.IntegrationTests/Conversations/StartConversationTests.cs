using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;

namespace SampleTwitter.API.IntegrationTests.Conversations;

public class StartConversationTests : IntegrationTestBase
{
    public StartConversationTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task StartConversation_WithValidRecipient_Returns201AndPersists()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");

        // Act
        var response = await SendStartRequest(aliceCookie, bob.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal(bob.Id, body.OtherParticipant.Id);
        Assert.Equal("bob", body.OtherParticipant.Username);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var conv = db.Conversations.Single(c => c.Id == body.Id);
        Assert.Equal(Math.Min(alice.Id, bob.Id), conv.ParticipantAId);
        Assert.Equal(Math.Max(alice.Id, bob.Id), conv.ParticipantBId);
    }

    [Fact]
    public async Task StartConversation_AlreadyExistsInEitherDirection_Returns200WithSameId()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");

        var firstResponse = await SendStartRequest(aliceCookie, bob.Id);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(first);

        // Act: same direction
        var repeatResponse = await SendStartRequest(aliceCookie, bob.Id);
        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var repeat = await repeatResponse.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.Equal(first.Id, repeat!.Id);

        // Act: reverse direction (Bob -> Alice)
        var reverseResponse = await SendStartRequest(bobCookie, alice.Id);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reverse = await reverseResponse.Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.Equal(first.Id, reverse!.Id);
        Assert.Equal(alice.Id, reverse.OtherParticipant.Id);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Single(db.Conversations);
    }

    [Fact]
    public async Task StartConversation_WithSelf_Returns400ProblemDetails()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendStartRequest(aliceCookie, alice.Id);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartConversation_WithNonExistentRecipient_Returns404ProblemDetails()
    {
        // Arrange
        var (_, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!");

        // Act
        var response = await SendStartRequest(aliceCookie, 999999);

        // Assert
        await response.AssertProblemDetails(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartConversation_Unauthenticated_Returns401()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/conversations", new StartConversationRequest { RecipientId = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StartConversation_ConcurrentRequestsBetweenSamePair_CreatesSingleRowAndReturnsSameId()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var (bob, bobCookie) = await SeedAndSignIn("bob@example.com", "Sup3rSecret1!", username: "bob");

        using var barrier = Factory.ConversationInsertBarrier.Arm(participants: 2, timeout: TimeSpan.FromSeconds(10));

        // Act: Alice -> Bob and Bob -> Alice race simultaneously
        var taskA = SendStartRequest(aliceCookie, bob.Id);
        var taskB = SendStartRequest(bobCookie, alice.Id);

        var responses = await Task.WhenAll(taskA, taskB);

        // Assert
        Assert.Equal(2, barrier.ArrivedCount);
        var statusCodes = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Created }, statusCodes);

        var bodyA = await responses[0].Content.ReadFromJsonAsync<ConversationResponse>();
        var bodyB = await responses[1].Content.ReadFromJsonAsync<ConversationResponse>();
        Assert.NotNull(bodyA);
        Assert.NotNull(bodyB);
        Assert.Equal(bodyA.Id, bodyB.Id);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Single(db.Conversations);
    }

    private Task<HttpResponseMessage> SendStartRequest(string cookie, long recipientId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/conversations")
        {
            Content = JsonContent.Create(new StartConversationRequest { RecipientId = recipientId }),
            Headers = { { "Cookie", cookie } }
        };
        return Client.SendAsync(message);
    }
}