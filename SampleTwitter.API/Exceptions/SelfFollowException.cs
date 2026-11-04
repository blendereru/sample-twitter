namespace SampleTwitter.API.Exceptions;

public class SelfFollowException : AppException
{
    public SelfFollowException(string internalMessage)
        : base(internalMessage, "You cannot follow yourself.", StatusCodes.Status400BadRequest, "Invalid follow operation")
    {
    }
}