namespace SampleTwitter.API.Exceptions;

public class EmptyMessageException : AppException
{
    public EmptyMessageException(string internalMessage)
        : base(internalMessage, "Message text cannot be empty.", StatusCodes.Status400BadRequest, "Empty message")
    {
    }
}