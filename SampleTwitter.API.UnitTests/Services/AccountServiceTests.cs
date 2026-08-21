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
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _emailConfirmationServiceMock = new Mock<IEmailConfirmationService>();

        _sut = new AccountService(
            _applicationContext,
            _emailConfirmationServiceMock.Object,
            _passwordHasherMock.Object,
            NullLogger<AccountService>.Instance);
    }
    
    [Fact]
    public async Task Register_NewUser_CallsSendConfirmationEmailWithNormalizedEmail()
    {
        // Arrange
        var request = new SignUpRequest { Email = "  TEST@EXAMPLE.COM  ", Password = "Sup3rSecret1!" };

        // Act
        await _sut.Register(request);

        // Assert
        _emailConfirmationServiceMock.Verify(
            s => s.SendConfirmationEmail(
                It.Is<User>(u => u.Email == "test@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task Register_NewUser_DelegatesHashingToPasswordHasher()
    {
        // Arrange
        var request = new SignUpRequest { Email = "new@example.com", Password = "Sup3rSecret1!" };

        // Act
        await _sut.Register(request);

        // Assert
        _passwordHasherMock.Verify(h => h.Hash("Sup3rSecret1!"), Times.Once);
    }
    
    [Fact]
    public async Task Register_ConfirmedUserExists_NeverCallsSendConfirmationEmail()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "existing@example.com", PasswordHash = "hash",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();

        // Act
        try { await _sut.Register(new SignUpRequest { Email = "existing@example.com", Password = "P@ss1" }); }
        catch (ConflictException) { }

        // Assert
        _emailConfirmationServiceMock.Verify(
            s => s.SendConfirmationEmail(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
    
    [Fact]
    public async Task Register_UnconfirmedUserExists_HashesTheNewPassword()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "pending@example.com", PasswordHash = "old-hash",
            EmailConfirmed = false, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();

        // Act
        await _sut.Register(new SignUpRequest { Email = "pending@example.com", Password = "NewP@ss1!" });

        // Assert
        _passwordHasherMock.Verify(h => h.Hash("NewP@ss1!"), Times.Once);
    }

    public void Dispose() => _applicationContext.Dispose();
}