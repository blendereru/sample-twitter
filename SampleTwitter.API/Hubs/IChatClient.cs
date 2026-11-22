using SampleTwitter.API.DTOs.ResponseDTOs;

namespace SampleTwitter.API.Hubs;

public interface IChatClient
{
    Task MessageReceived(MessageResponse message);
    Task ConversationRead(ConversationReadDto receipt);
    Task TypingStarted(TypingDto typing);
}