using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.ResponseDTOs;
using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly IMessagingService _messagingService;

    public ChatHub(IMessagingService messagingService)
    {
        _messagingService = messagingService;
    }

    public async Task Typing(long conversationId)
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var currentUserId))
        {
            throw new HubException("Unauthorized.");
        }

        long otherParticipantId;
        try
        {
            otherParticipantId = await _messagingService.GetOtherParticipantId(
                conversationId,
                currentUserId,
                Context.ConnectionAborted);
        }
        catch (AppException ex)
        {
            throw new HubException(ex.PublicMessage);
        }

        await Clients.User(otherParticipantId.ToString())
            .TypingStarted(new TypingDto(conversationId, currentUserId));
    }
}