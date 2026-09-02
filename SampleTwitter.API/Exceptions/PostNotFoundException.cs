namespace SampleTwitter.API.Exceptions;

public class PostNotFoundException : AppException
{
    public PostNotFoundException(string internalMessage)
        : base(internalMessage, "The referenced post was not found.", StatusCodes.Status404NotFound, "Post not found")
    {
    }
}