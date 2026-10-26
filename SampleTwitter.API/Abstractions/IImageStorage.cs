namespace SampleTwitter.API.Abstractions;

public interface IImageStorage
{
    Task<string> Save(Stream content, string extension, CancellationToken ct = default);
}