namespace SampleTwitter.API.Exceptions;

public class FollowNotFoundException : AppException
{
    public FollowNotFoundException(string internalMessage)
        : base(internalMessage, "You are not following this user.", StatusCodes.Status404NotFound, "Follow not found")
    {
    }
}