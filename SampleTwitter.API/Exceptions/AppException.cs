namespace SampleTwitter.API.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public string PublicMessage { get; }

    protected AppException(string internalMessage, string publicMessage, int statusCode, string title)
        : base(internalMessage)
    {
        PublicMessage = publicMessage;
        StatusCode = statusCode;
        Title = title;
    }
}