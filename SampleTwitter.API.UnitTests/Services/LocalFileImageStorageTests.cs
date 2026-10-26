using Microsoft.Extensions.Logging.Abstractions;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Services;

namespace SampleTwitter.API.UnitTests.Services;

public class LocalFileImageStorageTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly LocalFileImageStorage _sut;

    public LocalFileImageStorageTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"sampletwitter-uploads-tests-{Guid.NewGuid():N}");
        _sut = new LocalFileImageStorage(_tempDirectory, NullLogger<LocalFileImageStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Save_WithValidPng_SavesFileAndReturnsRelativeUrl()
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03 };
        await using var stream = new MemoryStream(pngBytes);

        // Act
        var relativeUrl = await _sut.Save(stream, ".png");

        // Assert
        Assert.StartsWith("/uploads/", relativeUrl);
        Assert.EndsWith(".png", relativeUrl);

        var fileName = Path.GetFileName(relativeUrl);
        var savedFilePath = Path.Combine(_tempDirectory, fileName);
        Assert.True(File.Exists(savedFilePath));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(savedFilePath));
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData("JPG")]
    public async Task Save_WithValidJpeg_NormalizesExtensionAndSavesFile(string extension)
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        await using var stream = new MemoryStream(jpegBytes);

        // Act
        var relativeUrl = await _sut.Save(stream, extension);

        // Assert
        Assert.StartsWith("/uploads/", relativeUrl);
        Assert.EndsWith(extension.TrimStart('.').ToLowerInvariant(), relativeUrl);

        var savedFilePath = Path.Combine(_tempDirectory, Path.GetFileName(relativeUrl));
        Assert.True(File.Exists(savedFilePath));
    }

    [Theory]
    [InlineData(0x37)] // GIF87a
    [InlineData(0x39)] // GIF89a
    public async Task Save_WithValidGif_SavesFileAndReturnsRelativeUrl(byte versionByte)
    {
        // Arrange
        var gifBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, versionByte, 0x61, 0x01, 0x00, 0x01, 0x00 };
        await using var stream = new MemoryStream(gifBytes);

        // Act
        var relativeUrl = await _sut.Save(stream, ".gif");

        // Assert
        Assert.StartsWith("/uploads/", relativeUrl);
        Assert.EndsWith(".gif", relativeUrl);
    }

    [Fact]
    public async Task Save_WithValidWebp_SavesFileAndReturnsRelativeUrl()
    {
        // Arrange
        var webpBytes = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, // RIFF
            0x24, 0x00, 0x00, 0x00, // size
            0x57, 0x45, 0x42, 0x50, // WEBP
            0x56, 0x50, 0x38, 0x20
        };
        await using var stream = new MemoryStream(webpBytes);

        // Act
        var relativeUrl = await _sut.Save(stream, ".webp");

        // Assert
        Assert.StartsWith("/uploads/", relativeUrl);
        Assert.EndsWith(".webp", relativeUrl);
    }

    [Fact]
    public async Task Save_WithNullStream_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.Save(null!, ".png"));
    }

    [Fact]
    public async Task Save_WithEmptyStream_ThrowsInvalidImageException()
    {
        // Arrange
        await using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.Save(stream, ".png"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    [InlineData(".svg")]
    [InlineData(".bmp")]
    [InlineData(".txt")]
    public async Task Save_WithUnsupportedExtension_ThrowsInvalidImageException(string extension)
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await using var stream = new MemoryStream(pngBytes);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.Save(stream, extension));
    }

    [Fact]
    public async Task Save_WithMismatchedMagicBytes_ThrowsInvalidImageExceptionAndDoesNotSaveFile()
    {
        // Arrange
        var fakePngBytes = "This is a plain text file pretending to be a PNG"u8.ToArray();
        await using var stream = new MemoryStream(fakePngBytes);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidImageException>(() => _sut.Save(stream, ".png"));

        Assert.False(Directory.Exists(_tempDirectory) && Directory.EnumerateFiles(_tempDirectory).Any());
    }

    [Fact]
    public async Task Save_WithSeekableFileExceedingMaxLimit_ThrowsFileTooLongException()
    {
        // Arrange
        var oversizedBytes = new byte[LocalFileImageStorage.MaxFileSizeInBytes + 1];
        oversizedBytes[0] = 0xFF;
        oversizedBytes[1] = 0xD8;
        oversizedBytes[2] = 0xFF;
        await using var stream = new MemoryStream(oversizedBytes);

        // Act & Assert
        await Assert.ThrowsAsync<FileTooLongException>(() => _sut.Save(stream, ".jpg"));

        Assert.False(Directory.Exists(_tempDirectory) && Directory.EnumerateFiles(_tempDirectory).Any());
    }

    [Fact]
    public async Task Save_WithNonSeekableFileExceedingMaxLimit_ThrowsFileTooLongExceptionAndCleansUpPartialFile()
    {
        // Arrange
        var oversizedBytes = new byte[LocalFileImageStorage.MaxFileSizeInBytes + 100];
        oversizedBytes[0] = 0xFF;
        oversizedBytes[1] = 0xD8;
        oversizedBytes[2] = 0xFF;
        await using var innerStream = new MemoryStream(oversizedBytes);
        await using var nonSeekableStream = new NonSeekableReadStream(innerStream);

        // Act & Assert
        await Assert.ThrowsAsync<FileTooLongException>(() => _sut.Save(nonSeekableStream, ".jpg"));

        Assert.Empty(Directory.GetFiles(_tempDirectory));
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}