using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Services;

namespace SampleTwitter.API.UnitTests.Services;

public class SecureTokenGeneratorTests
{
    private readonly ISecureTokenGenerator _sut = new SecureTokenGenerator();

    [Fact]
    public void Generate_ProducesANonEmptyToken()
    {
        // Act
        var token = _sut.Generate();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void Generate_CalledMultipleTimes_ProducesUniqueTokens()
    {
        // Arrange & Act
        var tokens = Enumerable.Range(0, 1000)
            .Select(_ => _sut.Generate())
            .ToList();

        // Assert
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    [Fact]
    public void Generate_ProducesUrlSafeTokens()
    {
        // Act
        var token = _sut.Generate();

        // Assert
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Hash_IsDeterministic_SameInputProducesSameOutput()
    {
        // Act
        var token = _sut.Generate();

        var hash1 = _sut.Hash(token);
        var hash2 = _sut.Hash(token);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentInputsProduceDifferentOutputs()
    {
        // Act
        var hash1 = _sut.Hash("token-one");
        var hash2 = _sut.Hash("token-two");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_NeverReturnsTheRawInputValue()
    {
        // Act
        var token = _sut.Generate();

        var hash = _sut.Hash(token);

        // Assert
        Assert.NotEqual(token, hash);
    }
}