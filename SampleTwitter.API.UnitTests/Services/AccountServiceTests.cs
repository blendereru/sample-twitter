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
    [Fact]
    public async Task Login_ValidCredentials_ReturnsLoginResultWithCorrectUserIdAndEmail()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "user@example.com", PasswordHash = "hashed-pw",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();
        _passwordHasherMock.Setup(h => h.Verify("MyPassword1!", "hashed-pw")).Returns(true);

        // Act
        var result = await _sut.Login(new LoginRequest { Email = "user@example.com", Password = "MyPassword1!" });

        // Assert
        Assert.Equal("user@example.com", result.Email);
        Assert.True(result.UserId > 0);
    }

    [Fact]
    public async Task Login_EmailWithWhitespaceAndMixedCase_NormalizesBeforeLookup()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "normalized@example.com", PasswordHash = "hashed-pw",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();
        _passwordHasherMock.Setup(h => h.Verify(It.IsAny<string>(), "hashed-pw")).Returns(true);

        // Act
        var result = await _sut.Login(new LoginRequest { Email = "  NORMALIZED@EXAMPLE.COM  ", Password = "pass" });

        // Assert
        Assert.Equal("normalized@example.com", result.Email);
    }

    [Fact]
    public async Task Login_ValidCredentials_DelegatesVerificationToPasswordHasher()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "user@example.com", PasswordHash = "stored-hash",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();
        _passwordHasherMock.Setup(h => h.Verify("MyPassword1!", "stored-hash")).Returns(true);

        // Act
        await _sut.Login(new LoginRequest { Email = "user@example.com", Password = "MyPassword1!" });

        // Assert
        _passwordHasherMock.Verify(h => h.Verify("MyPassword1!", "stored-hash"), Times.Once);
    }

    [Fact]
    public async Task Login_NonExistentUser_ThrowsInvalidCredentialsException()
    {
        // Arrange — no users seeded

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _sut.Login(new LoginRequest { Email = "nobody@example.com", Password = "pass" }));
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ThrowsEmailNotConfirmedException()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "unconfirmed@example.com", PasswordHash = "hashed-pw",
            EmailConfirmed = false, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<EmailNotConfirmedException>(
            () => _sut.Login(new LoginRequest { Email = "unconfirmed@example.com", Password = "pass" }));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "user@example.com", PasswordHash = "correct-hash",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();
        _passwordHasherMock.Setup(h => h.Verify("wrong-password", "correct-hash")).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _sut.Login(new LoginRequest { Email = "user@example.com", Password = "wrong-password" }));
    }

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsUserWithCorrectFields()
    {
        // Arrange
        _applicationContext.Users.Add(new User
        {
            Email = "user@example.com", PasswordHash = "hash",
            EmailConfirmed = true, RegisteredAt = DateTimeOffset.UtcNow
        });
        await _applicationContext.SaveChangesAsync();
        var seededUser = _applicationContext.Users.Single();

        // Act
        var result = await _sut.GetUserById(seededUser.Id);

        // Assert
        Assert.Equal(seededUser.Id, result.Id);
        Assert.Equal("user@example.com", result.Email);
    }

    [Fact]
    public async Task GetUserById_NonExistentId_ThrowsUserNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _sut.GetUserById(999));
    }

    public void Dispose() => _applicationContext.Dispose();
}