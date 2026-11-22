using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IMessagingService
{
    Task<StartConversationResult> StartConversation(
        long currentUserId,
        StartConversationRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConversationSummaryResult>> GetConversations(
        long currentUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<MessageResult>> GetMessages(
        long conversationId,
        long currentUserId,
        long? before = null,
        long? after = null,
        int limit = 30,
        CancellationToken ct = default);

    Task<MessageResult> SendMessage(
        long conversationId,
        long currentUserId,
        SendMessageRequest request,
        CancellationToken ct = default);

    Task MarkAsRead(
        long conversationId,
        long currentUserId,
        CancellationToken ct = default);

    Task<long> GetOtherParticipantId(
        long conversationId,
        long currentUserId,
        CancellationToken ct = default);
}
