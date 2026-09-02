namespace SampleTwitter.API.Exceptions;

public class EmptyPostException : AppException
{
    public EmptyPostException(string internalMessage)
        : base(internalMessage, "A post must contain at least text or an image.", StatusCodes.Status400BadRequest, "Empty post")
    {
    }
}