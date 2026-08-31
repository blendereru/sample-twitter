namespace SampleTwitter.API.Exceptions;

public class UserNotFoundException : AppException
{
    public UserNotFoundException(string internalMessage)
        : base(internalMessage, "The requested user was not found.", StatusCodes.Status404NotFound, "User not found")
    {
    }
}
