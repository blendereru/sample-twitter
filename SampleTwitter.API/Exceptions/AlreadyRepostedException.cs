namespace SampleTwitter.API.Exceptions;

public class AlreadyRepostedException : AppException
{
    public AlreadyRepostedException(string internalMessage)
        : base(internalMessage, "You have already reposted this post.", StatusCodes.Status409Conflict, "Conflict")
    {
    }
}