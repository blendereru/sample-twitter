namespace SampleTwitter.API.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string internalMessage)
        : base(internalMessage, "You do not have permission to perform this action.", StatusCodes.Status403Forbidden, "Forbidden")
    {
    }
}