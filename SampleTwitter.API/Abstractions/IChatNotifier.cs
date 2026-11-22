using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IChatNotifier
{
    Task MessageSent(MessageNotification notification, CancellationToken ct = default);
    Task ConversationRead(ConversationReadNotification notification, CancellationToken ct = default);
}
