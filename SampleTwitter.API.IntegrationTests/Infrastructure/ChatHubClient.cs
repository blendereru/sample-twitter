using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

public sealed class ChatHubClient : IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly Channel<MessageResponse> _messages = Channel.CreateUnbounded<MessageResponse>();
    private readonly Channel<JsonElement> _rawMessages = Channel.CreateUnbounded<JsonElement>();
    private readonly Channel<ConversationReadDto> _readReceipts = Channel.CreateUnbounded<ConversationReadDto>();
    private readonly Channel<TypingDto> _typingEvents = Channel.CreateUnbounded<TypingDto>();

    private ChatHubClient(HubConnection connection)
    {
        _connection = connection;

        _connection.On<MessageResponse>("MessageReceived", msg => _messages.Writer.TryWrite(msg));
        _connection.On<JsonElement>("MessageReceived", raw => _rawMessages.Writer.TryWrite(raw));
        _connection.On<ConversationReadDto>("ConversationRead", receipt => _readReceipts.Writer.TryWrite(receipt));
        _connection.On<TypingDto>("TypingStarted", typing => _typingEvents.Writer.TryWrite(typing));
    }

    public static HubConnection BuildConnection(ApiWebApplicationFactory factory, string? cookie)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                if (cookie is not null)
                {
                    o.Headers["Cookie"] = cookie;
                }
            })
            .Build();
    }

    public static async Task<ChatHubClient> ConnectAsync(ApiWebApplicationFactory factory, string cookie)
    {
        var connection = BuildConnection(factory, cookie);
        var client = new ChatHubClient(connection);
        await connection.StartAsync();
        return client;
    }

    public Task SendTypingAsync(long conversationId)
        => _connection.InvokeAsync("Typing", conversationId);

    public Task<MessageResponse> WaitForMessageAsync(TimeSpan? timeout = null)
        => ReadWithTimeoutAsync(_messages.Reader, "MessageReceived", timeout ?? DefaultTimeout);

    public Task<JsonElement> WaitForRawMessageAsync(TimeSpan? timeout = null)
        => ReadWithTimeoutAsync(_rawMessages.Reader, "MessageReceived (raw)", timeout ?? DefaultTimeout);

    public Task<ConversationReadDto> WaitForReadReceiptAsync(TimeSpan? timeout = null)
        => ReadWithTimeoutAsync(_readReceipts.Reader, "ConversationRead", timeout ?? DefaultTimeout);

    public Task<TypingDto> WaitForTypingAsync(TimeSpan? timeout = null)
        => ReadWithTimeoutAsync(_typingEvents.Reader, "TypingStarted", timeout ?? DefaultTimeout);

    private static async Task<T> ReadWithTimeoutAsync<T>(ChannelReader<T> reader, string eventName, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timed out after {timeout.TotalSeconds:F1}s waiting for SignalR event '{eventName}'.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}