using SampleTwitter.API.Models;
using SampleTwitter.API.Services;

namespace SampleTwitter.API.UnitTests.Services;

public class UsernameGeneratorTests
{
    private readonly UsernameGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsUsernameMatchingExpectedFormatAndPassingDomainValidation()
    {
        // Act
        var username = _generator.Generate();

        // Assert
        Assert.StartsWith(UsernameGenerator.Prefix, username);
        Assert.Equal(UsernameGenerator.Prefix.Length + UsernameGenerator.SuffixLength, username.Length);
        Assert.Matches("^user_[a-z0-9]{8}$", username);
        Assert.Null(User.GetUsernameValidationError(username));
    }

    [Fact]
    public void Generate_ProducesDistinctValuesAcrossInvocations()
    {
        // Act
        var generated = Enumerable.Range(0, 50).Select(_ => _generator.Generate()).ToHashSet();

        // Assert
        Assert.Equal(50, generated.Count);
    }
}