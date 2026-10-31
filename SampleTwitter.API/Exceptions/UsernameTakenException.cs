namespace SampleTwitter.API.Exceptions;

public class UsernameTakenException : AppException
{
    public UsernameTakenException(string internalMessage)
        : base(internalMessage, "This username is already taken.", StatusCodes.Status409Conflict, "Conflict")
    {
    }
}