namespace SampleTwitter.API.Exceptions;

public class FileTooLongException : AppException
{
    public FileTooLongException(string internalMessage) : base(internalMessage, "The file is too long.", StatusCodes.Status400BadRequest, "File too long")
    {
    }
}