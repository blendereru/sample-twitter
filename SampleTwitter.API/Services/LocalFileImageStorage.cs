using Microsoft.Extensions.DependencyInjection;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Services;

public class LocalFileImageStorage : IImageStorage
{
    public const long MaxFileSizeInBytes = 5 * 1024 * 1024;
    public const long MaxFileSizeInMegabytes = 5;
    private const int HeaderBufferSize = 12;
    private const int CopyBufferSize = 81920;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

    private readonly string _uploadsDirectory;
    private readonly ILogger<LocalFileImageStorage> _logger;

    [ActivatorUtilitiesConstructor]
    public LocalFileImageStorage(IWebHostEnvironment environment, ILogger<LocalFileImageStorage> logger)
    {
        _logger = logger;
        var webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        _uploadsDirectory = Path.Combine(webRootPath, "uploads");
    }

    public LocalFileImageStorage(string uploadsDirectory, ILogger<LocalFileImageStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadsDirectory);
        _uploadsDirectory = uploadsDirectory;
        _logger = logger;
    }

    public async Task<string> Save(Stream content, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalizedExtension = NormalizeAndValidateExtension(extension);

        if (content.CanSeek)
        {
            if (content.Length == 0)
            {
                throw new InvalidImageException("Uploaded image stream is empty.", "The uploaded file is empty.");
            }

            if (content.Length > MaxFileSizeInBytes)
            {
                throw new FileTooLongException(
                    $"File is too long ({content.Length} bytes). The maximum file size is {MaxFileSizeInMegabytes}MB.");
            }
        }

        var header = new byte[HeaderBufferSize];
        var headerBytesRead = await ReadHeaderAsync(content, header, ct);

        if (headerBytesRead == 0)
        {
            throw new InvalidImageException("Uploaded image stream is empty.", "The uploaded file is empty.");
        }

        if (!IsValidImageSignature(header.AsSpan(0, headerBytesRead), normalizedExtension))
        {
            throw new InvalidImageException(
                $"File signature does not match the expected format for extension '{normalizedExtension}'.");
        }

        Directory.CreateDirectory(_uploadsDirectory);

        var fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
        var physicalPath = Path.Combine(_uploadsDirectory, fileName);

        try
        {
            await using var fileStream = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: CopyBufferSize,
                useAsync: true);

            long totalBytesWritten = headerBytesRead;
            await fileStream.WriteAsync(header.AsMemory(0, headerBytesRead), ct);

            var buffer = new byte[CopyBufferSize];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, ct)) > 0)
            {
                totalBytesWritten += bytesRead;
                if (totalBytesWritten > MaxFileSizeInBytes)
                {
                    throw new FileTooLongException(
                        $"File is too long. The maximum file size is {MaxFileSizeInMegabytes}MB.");
                }

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            }
        }
        catch
        {
            TryDeleteFile(physicalPath);
            throw;
        }

        var relativeUrl = $"/uploads/{fileName}";
        _logger.LogInformation("Saved uploaded image to {RelativeUrl}", relativeUrl);
        return relativeUrl;
    }

    private static string NormalizeAndValidateExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new InvalidImageException("Image file extension is missing.");
        }

        var trimmed = extension.Trim().ToLowerInvariant();
        var normalized = trimmed.StartsWith('.') ? trimmed : $".{trimmed}";

        if (!AllowedExtensions.Contains(normalized))
        {
            throw new InvalidImageException($"Unsupported image extension '{extension}'.");
        }

        return normalized;
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] headerBuffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < headerBuffer.Length)
        {
            var read = await stream.ReadAsync(
                headerBuffer.AsMemory(totalRead, headerBuffer.Length - totalRead),
                ct);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }

        return totalRead;
    }

    private static bool IsValidImageSignature(ReadOnlySpan<byte> header, string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 &&
                                 header[0] == 0xFF &&
                                 header[1] == 0xD8 &&
                                 header[2] == 0xFF,

            ".png" => header.Length >= 8 &&
                      header[0] == 0x89 &&
                      header[1] == 0x50 &&
                      header[2] == 0x4E &&
                      header[3] == 0x47 &&
                      header[4] == 0x0D &&
                      header[5] == 0x0A &&
                      header[6] == 0x1A &&
                      header[7] == 0x0A,

            ".gif" => header.Length >= 6 &&
                      header[0] == 0x47 && // G
                      header[1] == 0x49 && // I
                      header[2] == 0x46 && // F
                      header[3] == 0x38 && // 8
                      (header[4] == 0x37 || header[4] == 0x39) && // 7 or 9
                      header[5] == 0x61,   // a

            ".webp" => header.Length >= 12 &&
                       header[0] == 0x52 && // R
                       header[1] == 0x49 && // I
                       header[2] == 0x46 && // F
                       header[3] == 0x46 && // F
                       header[8] == 0x57 && // W
                       header[9] == 0x45 && // E
                       header[10] == 0x42 && // B
                       header[11] == 0x50,   // P

            _ => false
        };
    }

    private void TryDeleteFile(string physicalPath)
    {
        try
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete partial upload file at {PhysicalPath}", physicalPath);
        }
    }
}
