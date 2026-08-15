using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Services;
using SampleTwitter.API.UnitTests.Helpers;

namespace SampleTwitter.API.UnitTests.Services;

public class AccountServiceTests : IDisposable
{
    private readonly ApplicationContext _applicationContext;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IEmailConfirmationService> _emailConfirmationServiceMock;
    private readonly IAccountService _sut;

    public AccountServiceTests()
    {
        _applicationContext = TestDbContextFactory.Create();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _emailConfirmationServiceMock = new Mock<IEmailConfirmationService>();

        _sut = new AccountService(
            _applicationContext,
            _emailConfirmationServiceMock.Object,
            _passwordHasherMock.Object,
            NullLogger<AccountService>.Instance);
    }
    
    [Fact]
    public async Task Register_WhenUserExists_ReturnsIsNotNewRegistration()
    {
        // Arrange
        var existingUser = new User
        {
            Email = "pending@example.com",
            PasswordHash = "old-hash",
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _applicationContext.Users.Add(existingUser);
        await _applicationContext.SaveChangesAsync();

        var request = new SignUpRequest { Email = "pending@example.com", Password = "NewPassword1!" };
        _passwordHasherMock.Setup(h => h.Hash(request.Password)).Returns("new-hash");

        // Act
        var result = await _sut.Register(request);

        // Assert
        Assert.False(result.IsNewRegistration);
    }
    
    [Fact]
    public async Task Register_WhenConfirmedUserExists_ThrowsConflictException()
    {
        // Arrange
        var confirmedUser = new User
        {
            Email = "existing@example.com",
            PasswordHash = "confirmed-hash",
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        _applicationContext.Users.Add(confirmedUser);
        await _applicationContext.SaveChangesAsync();

        var request = new SignUpRequest { Email = "existing@example.com", Password = "AttemptedPassword1!" };

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _sut.Register(request));
    }
    
    [Fact]
    public async Task Register_WhenConfirmedUserExists_DoesNotSendConfirmationEmail()
    {
        // Arrange
        var confirmedUser = new User
        {
            Email = "existing@example.com",
            PasswordHash = "confirmed-hash",
            EmailConfirmed = true,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        _applicationContext.Users.Add(confirmedUser);
        await _applicationContext.SaveChangesAsync();

        var request = new SignUpRequest { Email = "existing@example.com", Password = "AttemptedPassword1!" };

        // Act
        try
        {
            await _sut.Register(request);
        }
        catch (ConflictException)
        {
            // expected
        }

        // Assert
        _emailConfirmationServiceMock.Verify(
            s => s.SendConfirmationEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task Register_WhenUserDoesNotExist_ReturnsIsNewRegistrationTrue()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret!" };
        _passwordHasherMock.Setup(h => h.Hash(request.Password)).Returns("hashed-password");

        // Act
        var result = await _sut.Register(request);

        // Assert
        Assert.True(result.IsNewRegistration);
    }
    
    public void Dispose() => _applicationContext.Dispose();
}