namespace SampleTwitter.API.Exceptions;

public class ConversationNotFoundException : AppException
{
    public ConversationNotFoundException(string internalMessage)
        : base(internalMessage, "Conversation not found.", StatusCodes.Status404NotFound, "Conversation not found")
    {
    }
}