namespace SampleTwitter.API.Exceptions;

public class SelfMessageException : AppException
{
    public SelfMessageException(string internalMessage)
        : base(internalMessage, "You cannot start a conversation with yourself.", StatusCodes.Status400BadRequest, "Invalid conversation")
    {
    }
}