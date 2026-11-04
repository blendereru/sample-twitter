namespace SampleTwitter.API.Exceptions;

public class AlreadyFollowedException : AppException
{
    public AlreadyFollowedException(string internalMessage)
        : base(internalMessage, "You are already following this user.", StatusCodes.Status409Conflict, "Conflict")
    {
    }
}