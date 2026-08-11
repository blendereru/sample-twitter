namespace SampleTwitter.API.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string internalMessage)
        : base(internalMessage, "Registration could not be completed.", StatusCodes.Status409Conflict, "Conflict")
    {
    }
}