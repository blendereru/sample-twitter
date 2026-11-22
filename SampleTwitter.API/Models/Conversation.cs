using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class Conversation
{
    private readonly List<Message> _messages = new();

    public long Id { get; private set; }
    public long ParticipantAId { get; private set; }
    public User ParticipantA { get; private set; } = null!;
    public long ParticipantBId { get; private set; }
    public User ParticipantB { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastMessageAt { get; private set; }
    public DateTimeOffset? ParticipantALastReadAt { get; private set; }
    public DateTimeOffset? ParticipantBLastReadAt { get; private set; }
    public IReadOnlyCollection<Message> Messages => _messages;

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Start"/>.
    /// </summary>
    private Conversation()
    {
    }

    public static (long ParticipantAId, long ParticipantBId) OrderParticipants(long userId1, long userId2)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId2);

        if (userId1 == userId2)
        {
            throw new SelfMessageException($"User {userId1} attempted to start a conversation with themselves.");
        }

        return userId1 < userId2 ? (userId1, userId2) : (userId2, userId1);
    }

    public static Conversation Start(long initiatorId, long recipientId, DateTimeOffset? now = null)
    {
        var (participantAId, participantBId) = OrderParticipants(initiatorId, recipientId);

        return new Conversation
        {
            ParticipantAId = participantAId,
            ParticipantBId = participantBId,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }

    public bool IsParticipant(long userId) => userId == ParticipantAId || userId == ParticipantBId;

    public long GetOtherParticipantId(long userId)
    {
        if (userId == ParticipantAId)
        {
            return ParticipantBId;
        }

        if (userId == ParticipantBId)
        {
            return ParticipantAId;
        }

        throw new ForbiddenException($"User {userId} is not a participant in conversation {Id}.");
    }

    public DateTimeOffset? GetLastReadAt(long userId)
    {
        if (userId == ParticipantAId)
        {
            return ParticipantALastReadAt;
        }

        if (userId == ParticipantBId)
        {
            return ParticipantBLastReadAt;
        }

        throw new ForbiddenException($"User {userId} is not a participant in conversation {Id}.");
    }

    public Message SendMessage(long senderId, string? text, DateTimeOffset? now = null)
    {
        EnsureParticipant(senderId);

        var normalizedText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        if (normalizedText is null)
        {
            throw new EmptyMessageException($"User {senderId} attempted to send an empty message in conversation {Id}.");
        }

        if (normalizedText.Length > Message.MaxTextLength)
        {
            throw new ArgumentException(
                $"Message text cannot exceed {Message.MaxTextLength} characters.",
                nameof(text));
        }

        var sentAt = now ?? DateTimeOffset.UtcNow;
        var message = Message.Create(Id, senderId, normalizedText, sentAt);

        _messages.Add(message);
        LastMessageAt = sentAt;
        AdvanceReadMarker(senderId, sentAt);

        return message;
    }

    public void MarkAsRead(long readerId, DateTimeOffset? now = null)
    {
        EnsureParticipant(readerId);

        var readAt = now ?? DateTimeOffset.UtcNow;
        AdvanceReadMarker(readerId, readAt);

        foreach (var message in _messages)
        {
            if (message.SenderId != readerId && message.ReadAt is null && message.SentAt <= readAt)
            {
                message.MarkRead(readAt);
            }
        }
    }

    private void EnsureParticipant(long userId)
    {
        if (!IsParticipant(userId))
        {
            throw new ForbiddenException($"User {userId} is not a participant in conversation {Id}.");
        }
    }

    private void AdvanceReadMarker(long userId, DateTimeOffset timestamp)
    {
        if (userId == ParticipantAId)
        {
            if (ParticipantALastReadAt is null || timestamp > ParticipantALastReadAt)
            {
                ParticipantALastReadAt = timestamp;
            }
        }
        else if (userId == ParticipantBId)
        {
            if (ParticipantBLastReadAt is null || timestamp > ParticipantBLastReadAt)
            {
                ParticipantBLastReadAt = timestamp;
            }
        }
    }
}