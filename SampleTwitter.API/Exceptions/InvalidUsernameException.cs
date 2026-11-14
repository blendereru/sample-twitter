namespace SampleTwitter.API.Exceptions;

public class InvalidUsernameException : AppException
{
    public InvalidUsernameException(string internalMessage, string publicMessage)
        : base(internalMessage, publicMessage, StatusCodes.Status400BadRequest, "Invalid username")
    {
    }
}