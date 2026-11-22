using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.IntegrationTests.Infrastructure;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.IntegrationTests.Realtime;

public class NotifierFailureTests : IntegrationTestBase
{
    public NotifierFailureTests(ApiWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task SendMessage_WhenNotifierThrows_StillReturns201AndPersistsMessage()
    {
        // Arrange
        var (alice, aliceCookie) = await SeedAndSignIn("alice@example.com", "Sup3rSecret1!", username: "alice");
        var bob = await SeedUser("bob@example.com", "Sup3rSecret1!", username: "bob");
        var conversation = await SeedConversation(alice.Id, bob.Id);

        using var failingFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatNotifier>();
                services.AddScoped<IChatNotifier, ThrowingChatNotifier>();
            });
        });

        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversation.Id}/messages")
        {
            Content = JsonContent.Create(new SendMessageRequest { Text = "Persisted despite push failure" }),
            Headers = { { "Cookie", aliceCookie } }
        };

        // Act
        var response = await failingClient.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(body);
        Assert.Equal("Persisted despite push failure", body.Text);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Single(db.Messages, m => m.Id == body.Id && m.Text == "Persisted despite push failure");
    }

    private sealed class ThrowingChatNotifier : IChatNotifier
    {
        public Task MessageSent(MessageNotification notification, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated SignalR push failure.");

        public Task ConversationRead(ConversationReadNotification notification, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated SignalR push failure.");
    }
}