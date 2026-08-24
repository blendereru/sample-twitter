using SampleTwitter.API.Services;

namespace SampleTwitter.API.UnitTests.Services;

public class SecureTokenGeneratorTests
{
    private readonly SecureTokenGenerator _sut = new();

    
    [Fact]
    public void Generate_OutputContainsNoStandardBase64Characters()
    {
        // Act
        var tokens = Enumerable.Range(0, 50)
            .Select(_ => _sut.Generate())
            .ToList();

        // Assert
        Assert.All(tokens, token =>
        {
            Assert.DoesNotContain('+', token);
            Assert.DoesNotContain('/', token);
            Assert.DoesNotContain('=', token);
        });
    }
}