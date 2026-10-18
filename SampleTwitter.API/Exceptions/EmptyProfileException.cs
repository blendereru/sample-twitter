namespace SampleTwitter.API.Exceptions;

public class EmptyProfileException : AppException
{
    public EmptyProfileException(string internalMessage)
        : base(internalMessage, "A profile must contain at least a username or an image.", StatusCodes.Status400BadRequest, "Empty profile")
    {
    }
}
