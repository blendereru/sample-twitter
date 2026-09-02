namespace SampleTwitter.API.Exceptions;

public class InvalidCredentialsException : AppException
{
    public InvalidCredentialsException(string internalMessage)
        : base(internalMessage, "Invalid email or password.", StatusCodes.Status401Unauthorized, "Authentication failed")
    {
    }
}