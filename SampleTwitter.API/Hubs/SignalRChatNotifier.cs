using Microsoft.AspNetCore.SignalR;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Hubs;

public class SignalRChatNotifier : IChatNotifier
{
    private readonly IHubContext<ChatHub, IChatClient> _hubContext;

    public SignalRChatNotifier(IHubContext<ChatHub, IChatClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task MessageSent(MessageNotification notification, CancellationToken ct = default)
    {
        var dto = new MessageResponse(
            notification.Id,
            notification.ConversationId,
            notification.SenderId,
            notification.SenderUsername,
            notification.Text,
            notification.SentAt);

        return _hubContext.Clients
            .Users(notification.RecipientId.ToString(), notification.SenderId.ToString())
            .MessageReceived(dto);
    }

    public Task ConversationRead(ConversationReadNotification notification, CancellationToken ct = default)
    {
        var dto = new ConversationReadDto(
            notification.ConversationId,
            notification.ReaderId,
            notification.ReadAt);

        return _hubContext.Clients
            .User(notification.RecipientId.ToString())
            .ConversationRead(dto);
    }
}