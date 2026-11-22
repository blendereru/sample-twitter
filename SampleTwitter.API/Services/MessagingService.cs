using Microsoft.EntityFrameworkCore;
using Npgsql;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Services;

public class MessagingService : IMessagingService
{
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 50;

    private readonly ApplicationContext _applicationContext;
    private readonly IChatNotifier _chatNotifier;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        ApplicationContext applicationContext,
        IChatNotifier chatNotifier,
        ILogger<MessagingService> logger)
    {
        _applicationContext = applicationContext;
        _chatNotifier = chatNotifier;
        _logger = logger;
    }

    public async Task<StartConversationResult> StartConversation(
        long currentUserId,
        StartConversationRequest request,
        CancellationToken ct = default)
    {
        var (participantAId, participantBId) = Conversation.OrderParticipants(currentUserId, request.RecipientId);

        var recipient = await _applicationContext.Users
            .AsNoTracking()
            .Where(u => u.Id == request.RecipientId)
            .Select(u => new ConversationParticipantResult(u.Id, u.Username, u.ImageUrl))
            .SingleOrDefaultAsync(ct);

        if (recipient is null)
        {
            _logger.LogWarning("Start conversation failed — recipient user {RecipientId} not found", request.RecipientId);
            throw new UserNotFoundException($"User with id {request.RecipientId} was not found.");
        }

        var existing = await _applicationContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.ParticipantAId == participantAId && c.ParticipantBId == participantBId,
                ct);

        if (existing is not null)
        {
            return new StartConversationResult(
                existing.Id,
                recipient,
                existing.CreatedAt,
                existing.LastMessageAt,
                IsNew: false);
        }

        var conversation = Conversation.Start(currentUserId, request.RecipientId);
        _applicationContext.Conversations.Add(conversation);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                ex,
                "Race condition: concurrent conversation start detected between users {ParticipantAId} and {ParticipantBId}",
                participantAId,
                participantBId);

            _applicationContext.Entry(conversation).State = EntityState.Detached;

            var racedExisting = await _applicationContext.Conversations
                .AsNoTracking()
                .SingleAsync(
                    c => c.ParticipantAId == participantAId && c.ParticipantBId == participantBId,
                    ct);

            return new StartConversationResult(
                racedExisting.Id,
                recipient,
                racedExisting.CreatedAt,
                racedExisting.LastMessageAt,
                IsNew: false);
        }

        _logger.LogInformation(
            "Conversation {ConversationId} started between users {ParticipantAId} and {ParticipantBId}",
            conversation.Id,
            participantAId,
            participantBId);

        return new StartConversationResult(
            conversation.Id,
            recipient,
            conversation.CreatedAt,
            conversation.LastMessageAt,
            IsNew: true);
    }

    public async Task<IReadOnlyList<ConversationSummaryResult>> GetConversations(
        long currentUserId,
        CancellationToken ct = default)
    {
        var conversations = await _applicationContext.Conversations
            .AsNoTracking()
            .Where(c => c.ParticipantAId == currentUserId || c.ParticipantBId == currentUserId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Select(c => new ConversationSummaryResult(
                c.Id,
                c.ParticipantAId == currentUserId
                    ? new ConversationParticipantResult(c.ParticipantB.Id, c.ParticipantB.Username, c.ParticipantB.ImageUrl)
                    : new ConversationParticipantResult(c.ParticipantA.Id, c.ParticipantA.Username, c.ParticipantA.ImageUrl),
                c.CreatedAt,
                c.LastMessageAt,
                c.Messages
                    .OrderByDescending(m => m.SentAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => new MessageResult(
                        m.Id,
                        m.ConversationId,
                        m.SenderId,
                        m.Sender.Username,
                        m.Text,
                        m.SentAt,
                        m.ReadAt))
                    .FirstOrDefault(),
                c.Messages.Count(m =>
                    m.SenderId != currentUserId &&
                    ((c.ParticipantAId == currentUserId ? c.ParticipantALastReadAt : c.ParticipantBLastReadAt) == null ||
                     m.SentAt > (c.ParticipantAId == currentUserId ? c.ParticipantALastReadAt : c.ParticipantBLastReadAt))),
                c.ParticipantAId == currentUserId
                    ? c.ParticipantBLastReadAt
                    : c.ParticipantALastReadAt))
            .ToListAsync(ct);

        return conversations;
    }

    public async Task<IReadOnlyList<MessageResult>> GetMessages(
        long conversationId,
        long currentUserId,
        long? before = null,
        long? after = null,
        int limit = DefaultPageSize,
        CancellationToken ct = default)
    {
        var conversation = await _applicationContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Get messages failed — conversation {ConversationId} not found", conversationId);
            throw new ConversationNotFoundException($"Conversation with id {conversationId} was not found.");
        }

        if (!conversation.IsParticipant(currentUserId))
        {
            _logger.LogWarning(
                "Get messages forbidden — user {UserId} is not a participant in conversation {ConversationId}",
                currentUserId,
                conversationId);
            throw new ForbiddenException($"User {currentUserId} is not a participant in conversation {conversationId}.");
        }

        var pageSize = Math.Clamp(limit, 1, MaxPageSize);

        var query = _applicationContext.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (after.HasValue)
        {
            return await query
                .Where(m => m.Id > after.Value)
                .OrderBy(m => m.SentAt)
                .ThenBy(m => m.Id)
                .Take(pageSize)
                .Select(m => new MessageResult(
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    m.Sender.Username,
                    m.Text,
                    m.SentAt,
                    m.ReadAt))
                .ToListAsync(ct);
        }

        if (before.HasValue)
        {
            query = query.Where(m => m.Id < before.Value);
        }

        return await query
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize)
            .Select(m => new MessageResult(
                m.Id,
                m.ConversationId,
                m.SenderId,
                m.Sender.Username,
                m.Text,
                m.SentAt,
                m.ReadAt))
            .ToListAsync(ct);
    }

    public async Task<MessageResult> SendMessage(
        long conversationId,
        long currentUserId,
        SendMessageRequest request,
        CancellationToken ct = default)
    {
        var conversation = await _applicationContext.Conversations
            .SingleOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Send message failed — conversation {ConversationId} not found", conversationId);
            throw new ConversationNotFoundException($"Conversation with id {conversationId} was not found.");
        }

        var message = conversation.SendMessage(currentUserId, request.Text);

        var senderUsername = await _applicationContext.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => u.Username)
            .SingleAsync(ct);

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Message {MessageId} sent in conversation {ConversationId} by user {UserId}",
            message.Id,
            conversationId,
            currentUserId);

        var recipientId = conversation.GetOtherParticipantId(currentUserId);

        try
        {
            await _chatNotifier.MessageSent(
                new MessageNotification(
                    message.Id,
                    message.ConversationId,
                    message.SenderId,
                    senderUsername,
                    recipientId,
                    message.Text,
                    message.SentAt),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push real-time notification for message {MessageId} in conversation {ConversationId}",
                message.Id,
                conversationId);
        }

        return new MessageResult(
            message.Id,
            message.ConversationId,
            message.SenderId,
            senderUsername,
            message.Text,
            message.SentAt);
    }

    public async Task MarkAsRead(long conversationId, long currentUserId, CancellationToken ct = default)
    {
        var conversation = await _applicationContext.Conversations
            .Include(c => c.Messages.Where(m => m.SenderId != currentUserId && m.ReadAt == null))
            .SingleOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Mark as read failed — conversation {ConversationId} not found", conversationId);
            throw new ConversationNotFoundException($"Conversation with id {conversationId} was not found.");
        }

        conversation.MarkAsRead(currentUserId);
        await _applicationContext.SaveChangesAsync(ct);

        var readAt = conversation.GetLastReadAt(currentUserId)!.Value;
        var recipientId = conversation.GetOtherParticipantId(currentUserId);

        try
        {
            await _chatNotifier.ConversationRead(
                new ConversationReadNotification(conversationId, currentUserId, recipientId, readAt),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to push read receipt notification for conversation {ConversationId} by user {UserId}",
                conversationId,
                currentUserId);
        }
    }

    public async Task<long> GetOtherParticipantId(long conversationId, long currentUserId, CancellationToken ct = default)
    {
        var conversation = await _applicationContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
        {
            throw new ConversationNotFoundException($"Conversation with id {conversationId} was not found.");
        }

        return conversation.GetOtherParticipantId(currentUserId);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
               && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}