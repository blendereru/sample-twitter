namespace SampleTwitter.API.Exceptions;

public class InvalidImageException : AppException
{
    public InvalidImageException(
        string internalMessage,
        string publicMessage = "The uploaded file is not a valid image. Allowed formats: JPG, PNG, GIF, WEBP.")
        : base(internalMessage, publicMessage, StatusCodes.Status400BadRequest, "Invalid image")
    {
    }
}