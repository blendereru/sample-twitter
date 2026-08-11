namespace SampleTwitter.API.Exceptions;

public class InvalidTokenException : AppException
{
    public InvalidTokenException(string internalMessage)
        : base(internalMessage, "This confirmation link is invalid or has expired.", StatusCodes.Status400BadRequest, "Invalid token")
    {
    }
}