using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Services;

namespace SampleTwitter.API.UnitTests.Services;

public class BCryptPasswordHasherTests
{
    private readonly IPasswordHasher _sut = new BCryptPasswordHasher();

    [Fact]
    public void Hash_ProducesADifferentStringThanTheOriginalPassword()
    {
        // Act
        var hash = _sut.Hash("MyPassword1!");

        // Assert
        Assert.NotEqual("MyPassword1!", hash);
    }

    [Fact]
    public void Hash_CalledTwiceWithSamePassword_ProducesDifferentHashes()
    {
        // Act
        var hash1 = _sut.Hash("MyPassword1!");
        var hash2 = _sut.Hash("MyPassword1!");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Act
        var hash = _sut.Hash("MyPassword1!");

        // Assert
        Assert.True(_sut.Verify("MyPassword1!", hash));
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Act
        var hash = _sut.Hash("MyPassword1!");

        // Assert
        Assert.False(_sut.Verify("WrongPassword!", hash));
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        // Act
        var hash = _sut.Hash("MyPassword1!");

        // Assert
        Assert.False(_sut.Verify("mypassword1!", hash));
    }
}