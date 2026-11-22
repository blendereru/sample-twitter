using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Controllers;

[Tags("Conversations")]
[Route("api/conversations")]
[ApiController]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMessagingService _messagingService;

    public ConversationsController(IMessagingService messagingService)
    {
        _messagingService = messagingService;
    }

    /// <summary>
    /// Starts a 1:1 conversation with another user, or returns the existing conversation if one already exists.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartConversation(StartConversationRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _messagingService.StartConversation(currentUserId, request, ct);

        var response = new ConversationResponse(
            result.Id,
            new ConversationParticipantDto(
                result.OtherParticipant.Id,
                result.OtherParticipant.Username,
                result.OtherParticipant.ImageUrl),
            result.CreatedAt,
            result.LastMessageAt,
            LastMessage: null,
            UnreadCount: 0,
            OtherParticipantLastReadAt: null);

        return result.IsNew
            ? CreatedAtAction(nameof(GetMessages), new { id = result.Id }, response)
            : Ok(response);
    }

    /// <summary>
    /// Returns the authenticated user's conversation inbox ordered by the most recent activity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetConversations(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();

        var results = await _messagingService.GetConversations(currentUserId, ct);

        var response = results.Select(c => new ConversationResponse(
            c.Id,
            new ConversationParticipantDto(
                c.OtherParticipant.Id,
                c.OtherParticipant.Username,
                c.OtherParticipant.ImageUrl),
            c.CreatedAt,
            c.LastMessageAt,
            c.LastMessage is null
                ? null
                : new MessageResponse(
                    c.LastMessage.Id,
                    c.LastMessage.ConversationId,
                    c.LastMessage.SenderId,
                    c.LastMessage.SenderUsername,
                    c.LastMessage.Text,
                    c.LastMessage.SentAt),
            c.UnreadCount,
            c.OtherParticipantLastReadAt)).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Returns paginated messages for a conversation.
    /// </summary>
    [HttpGet("{id:long}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        long id,
        [FromQuery] long? before = null,
        [FromQuery] long? after = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();

        var messages = await _messagingService.GetMessages(id, currentUserId, before, after, limit, ct);

        var response = messages.Select(m => new MessageResponse(
            m.Id,
            m.ConversationId,
            m.SenderId,
            m.SenderUsername,
            m.Text,
            m.SentAt)).ToList();

        return Ok(response);
    }

    /// <summary>
    /// Sends a message in an existing conversation.
    /// </summary>
    [HttpPost("{id:long}/messages")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(long id, SendMessageRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();

        var result = await _messagingService.SendMessage(id, currentUserId, request, ct);

        var response = new MessageResponse(
            result.Id,
            result.ConversationId,
            result.SenderId,
            result.SenderUsername,
            result.Text,
            result.SentAt);

        return CreatedAtAction(nameof(GetMessages), new { id = result.ConversationId }, response);
    }

    /// <summary>
    /// Marks all messages in a conversation as read for the authenticated user.
    /// </summary>
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();

        await _messagingService.MarkAsRead(id, currentUserId, ct);

        return NoContent();
    }

    private long GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.Parse(userIdClaim!);
    }
}