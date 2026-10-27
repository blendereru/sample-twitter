namespace SampleTwitter.API.Exceptions;

public class AlreadyBookmarkedException : AppException
{
    public AlreadyBookmarkedException(string internalMessage)
        : base(internalMessage, "You have already bookmarked this post.", StatusCodes.Status409Conflict, "Conflict")
    {
    }
}