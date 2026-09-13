namespace SampleTwitter.API.Exceptions;

public class RepostNotFoundException : AppException
{
    public RepostNotFoundException(string internalMessage)
        : base(internalMessage, "The referenced repost was not found.", StatusCodes.Status404NotFound, "Repost not found")
    { }
}