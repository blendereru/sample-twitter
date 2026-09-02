namespace SampleTwitter.API.Exceptions;

public class EmailNotConfirmedException : AppException
{
    public EmailNotConfirmedException(string internalMessage)
        : base(internalMessage, "Please confirm your email address before signing in.", StatusCodes.Status403Forbidden, "Email not confirmed")
    {
    }
}