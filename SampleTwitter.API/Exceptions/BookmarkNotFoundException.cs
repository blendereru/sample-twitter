namespace SampleTwitter.API.Exceptions;

public class BookmarkNotFoundException : AppException
{
    public BookmarkNotFoundException(string internalMessage)
        : base(internalMessage, "The referenced bookmark was not found.", StatusCodes.Status404NotFound, "Bookmark not found")
    {
    }
}