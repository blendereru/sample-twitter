using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Services;
using SampleTwitter.API.UnitTests.Helpers;

namespace SampleTwitter.API.UnitTests.Services;

public class EmailConfirmationServiceTests : IDisposable
{
    private readonly ApplicationContext _applicationContext;
    private readonly Mock<ISecureTokenGenerator> _tokenGeneratorMock;
    private readonly Mock<IEmailSender> _emailSenderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly IEmailConfirmationService _sut;

    public EmailConfirmationServiceTests()
    {
        _applicationContext = TestDbContextFactory.Create();
        _tokenGeneratorMock = new Mock<ISecureTokenGenerator>();
        _emailSenderMock = new Mock<IEmailSender>();
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["AppSettings:BaseUrl"]).Returns("https://sampletwitter.com");

        _sut = new EmailConfirmationService(
            _applicationContext,
            _tokenGeneratorMock.Object,
            _emailSenderMock.Object,
            _configurationMock.Object,
            NullLogger<EmailConfirmationService>.Instance);
    }
    
    [Fact]
    public async Task SendConfirmationEmail_SendsEmailToTheUsersAddress()
    {
        // Arrange
        var user = UserCreationHelpers.CreateUser(email: "recipient@example.com");
        _tokenGeneratorMock.Setup(g => g.Generate()).Returns("raw-token");
        _tokenGeneratorMock.Setup(g => g.Hash("raw-token")).Returns("hashed-token");

        // Act
        await _sut.SendConfirmationEmail(user);

        // Assert
        _emailSenderMock.Verify(
            s => s.Send("recipient@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task SendConfirmationEmail_EmailBodyContainsTheRawTokenNotTheHash()
    {
        // Arrange
        var user = UserCreationHelpers.CreateUser();
        _tokenGeneratorMock.Setup(g => g.Generate()).Returns("raw-token-abc");
        _tokenGeneratorMock.Setup(g => g.Hash("raw-token-abc")).Returns("hashed-token-xyz");

        string? capturedBody = null;
        _emailSenderMock
            .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body);

        // Act
        await _sut.SendConfirmationEmail(user);

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("raw-token-abc", capturedBody);
        Assert.DoesNotContain("hashed-token-xyz", capturedBody);
    }
    
    [Fact]
    public async Task SendConfirmationEmail_EmailBodyContainsTheUsersId()
    {
        // Arrange
        var user = UserCreationHelpers.CreateUser(id: 42);
        _tokenGeneratorMock.Setup(g => g.Generate()).Returns("raw-token");
        _tokenGeneratorMock.Setup(g => g.Hash("raw-token")).Returns("hashed-token");

        string? capturedBody = null;
        _emailSenderMock
            .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body);

        // Act
        await _sut.SendConfirmationEmail(user);

        // Assert
        Assert.NotNull(capturedBody);
        Assert.Contains("userId=42", capturedBody);
    }
    
    [Fact]
    public async Task SendConfirmationEmail_WhenEmailSenderThrows_PropagatesTheException()
    {
        // Arrange
        var user = UserCreationHelpers.CreateUser();
        _tokenGeneratorMock.Setup(g => g.Generate()).Returns("raw-token");
        _tokenGeneratorMock.Setup(g => g.Hash("raw-token")).Returns("hashed-token");

        _emailSenderMock
            .Setup(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryException("SMTP down"));

        // Act & Assert
        await Assert.ThrowsAsync<EmailDeliveryException>(() => _sut.SendConfirmationEmail(user));
    }

    public void Dispose() => _applicationContext.Dispose();
}