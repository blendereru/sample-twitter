using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.UnitTests.Models;

public class UserTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("  USER@Example.COM  ", "user@example.com")]
    [InlineData("\tAlice.Smith@Domain.Org\n", "alice.smith@domain.org")]
    public void NormalizeEmail_WithValidEmail_TrimsAndLowercasesInvariant(string input, string expected)
    {
        // Act
        var normalized = User.NormalizeEmail(input);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_WhenNullOrWhitespace_ThrowsArgumentException(string? invalidEmail)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => User.NormalizeEmail(invalidEmail!));
    }

    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("  Alice_99  ", "alice_99")]
    [InlineData("\tBob_Jones\n", "bob_jones")]
    public void NormalizeUsername_WithValidUsername_TrimsAndLowercasesInvariant(string input, string expected)
    {
        // Act
        var normalized = User.NormalizeUsername(input);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeUsername_WhenNullOrWhitespace_ThrowsArgumentException(string? invalidUsername)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => User.NormalizeUsername(invalidUsername!));
    }

    [Fact]
    public void Register_WithValidArguments_CreatesUnconfirmedUserWithNormalizedEmailAndUsername()
    {
        // Act
        var user = User.Register("  New.User@Example.COM  ", "hashed-pw", "  New_User_99  ", FixedNow);

        // Assert
        Assert.Equal("new.user@example.com", user.Email);
        Assert.Equal("hashed-pw", user.PasswordHash);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(FixedNow, user.RegisteredAt);
        Assert.Equal("New_User_99", user.Username);
        Assert.Equal("new_user_99", user.NormalizedUsername);
        Assert.Null(user.ImageUrl);
        Assert.Null(user.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WhenPasswordHashIsBlank_ThrowsArgumentException(string? invalidHash)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            User.Register("user@example.com", invalidHash!, "valid_user", FixedNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("invalid-char")]
    [InlineData("invalid.dot")]
    [InlineData("has space")]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("settings")]
    public void Register_WhenUsernameIsInvalidOrReserved_ThrowsInvalidUsernameException(string? invalidUsername)
    {
        // Act & Assert
        Assert.Throws<InvalidUsernameException>(() =>
            User.Register("user@example.com", "hashed-pw", invalidUsername!, FixedNow));
    }

    [Fact]
    public void Register_WhenUsernameExceedsMaxLength_ThrowsInvalidUsernameException()
    {
        // Arrange
        var tooLong = new string('u', User.MaxUsernameLength + 1);

        // Act & Assert
        Assert.Throws<InvalidUsernameException>(() =>
            User.Register("user@example.com", "hashed-pw", tooLong, FixedNow));
    }

    [Fact]
    public void RenewRegistration_WhenUnconfirmedWithoutNewUsername_UpdatesPasswordHashAndKeepsExistingUsername()
    {
        // Arrange
        var user = User.Register("pending@example.com", "old-hash", "initial_handle", FixedNow);
        var renewTime = FixedNow.AddMinutes(30);

        // Act
        user.RenewRegistration("new-hash", null, renewTime);

        // Assert
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(renewTime, user.RegisteredAt);
        Assert.Equal("initial_handle", user.Username);
        Assert.Equal("initial_handle", user.NormalizedUsername);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public void RenewRegistration_WhenUnconfirmedWithNewUsername_UpdatesUsernameAndNormalizedUsername()
    {
        // Arrange
        var user = User.Register("pending@example.com", "old-hash", "initial_handle", FixedNow);
        var renewTime = FixedNow.AddMinutes(30);

        // Act
        user.RenewRegistration("new-hash", "  Updated_Handle  ", renewTime);

        // Assert
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(renewTime, user.RegisteredAt);
        Assert.Equal("Updated_Handle", user.Username);
        Assert.Equal("updated_handle", user.NormalizedUsername);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public void RenewRegistration_WhenAlreadyConfirmed_ThrowsConflictExceptionAndDoesNotMutateUser()
    {
        // Arrange
        var user = User.Register("confirmed@example.com", "existing-hash", "confirmed_user", FixedNow);
        user.ConfirmEmail();

        // Act & Assert
        Assert.Throws<ConflictException>(() =>
            user.RenewRegistration("attempted-hash", "new_handle", FixedNow.AddHours(1)));

        Assert.Equal("existing-hash", user.PasswordHash);
        Assert.Equal("confirmed_user", user.Username);
        Assert.Equal(FixedNow, user.RegisteredAt);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public void EnsureCanSignIn_WhenUnconfirmed_ThrowsEmailNotConfirmedException()
    {
        // Arrange
        var user = User.Register("pending@example.com", "hash", "pending_user", FixedNow);

        // Act & Assert
        Assert.Throws<EmailNotConfirmedException>(() => user.EnsureCanSignIn());
    }

    [Fact]
    public void ConfirmEmail_MarksEmailConfirmedAndAllowsSignIn()
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "valid_user", FixedNow);

        // Act
        user.ConfirmEmail();

        // Assert
        Assert.True(user.EmailConfirmed);
        var exception = Record.Exception(() => user.EnsureCanSignIn());
        Assert.Null(exception);
    }

    [Fact]
    public void UpdateProfile_WithUsernameAndImageUrl_TrimsAndUpdatesTimestampAndPreservesAccountFields()
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "initial_user", FixedNow);
        user.ConfirmEmail();
        var editTime = FixedNow.AddHours(2);

        // Act
        user.UpdateProfile("  alice_dev  ", "  https://example.com/avatar.png  ", editTime);

        // Assert
        Assert.Equal("alice_dev", user.Username);
        Assert.Equal("alice_dev", user.NormalizedUsername);
        Assert.Equal("https://example.com/avatar.png", user.ImageUrl);
        Assert.Equal(editTime, user.UpdatedAt);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("hash", user.PasswordHash);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(FixedNow, user.RegisteredAt);
    }

    [Fact]
    public void UpdateProfile_WithValidUsernameAndWhitespaceImageUrl_NormalizesImageUrlToNull()
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "initial_user", FixedNow);
        user.UpdateProfile("Alice", "https://example.com/old.png", FixedNow);

        // Act
        user.UpdateProfile("Alice", "   ", FixedNow.AddMinutes(5));

        // Assert
        Assert.Equal("Alice", user.Username);
        Assert.Equal("alice", user.NormalizedUsername);
        Assert.Null(user.ImageUrl);
    }

    [Fact]
    public void UpdateProfile_WithExactlyMinAndMaxUsernameLength_Succeeds()
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "initial_user", FixedNow);
        var minUsername = new string('a', User.MinUsernameLength);
        var maxUsername = new string('u', User.MaxUsernameLength);

        // Act & Assert (min)
        user.UpdateProfile($"  {minUsername}  ", null, FixedNow.AddMinutes(1));
        Assert.Equal(minUsername, user.Username);
        Assert.Equal(minUsername, user.NormalizedUsername);

        // Act & Assert (max)
        user.UpdateProfile($"  {maxUsername}  ", null, FixedNow.AddMinutes(2));
        Assert.Equal(maxUsername, user.Username);
        Assert.Equal(maxUsername.ToLowerInvariant(), user.NormalizedUsername);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("invalid-char")]
    [InlineData("admin")]
    [InlineData("SUPPORT")]
    public void UpdateProfile_WhenUsernameIsInvalidOrReserved_ThrowsInvalidUsernameExceptionAndDoesNotMutate(
        string? invalidUsername)
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "InitialName", FixedNow);
        user.UpdateProfile("InitialName", "https://example.com/initial.png", FixedNow);

        // Act & Assert
        Assert.Throws<InvalidUsernameException>(() =>
            user.UpdateProfile(invalidUsername!, "https://example.com/new.png", FixedNow.AddMinutes(10)));

        Assert.Equal("InitialName", user.Username);
        Assert.Equal("initialname", user.NormalizedUsername);
        Assert.Equal("https://example.com/initial.png", user.ImageUrl);
        Assert.Equal(FixedNow, user.UpdatedAt);
    }

    [Fact]
    public void UpdateProfile_WhenUsernameExceedsMaxLength_ThrowsInvalidUsernameExceptionAndDoesNotMutate()
    {
        // Arrange
        var user = User.Register("user@example.com", "hash", "InitialName", FixedNow);
        user.UpdateProfile("InitialName", null, FixedNow);
        var tooLong = new string('x', User.MaxUsernameLength + 1);

        // Act & Assert
        Assert.Throws<InvalidUsernameException>(() =>
            user.UpdateProfile(tooLong, null, FixedNow.AddMinutes(10)));

        Assert.Equal("InitialName", user.Username);
        Assert.Equal("initialname", user.NormalizedUsername);
    }

    [Fact]
    public void ChangePassword_UpdatesPasswordHashAndTimestampAndPreservesOtherFields()
    {
        // Arrange
        var user = User.Register("user@example.com", "old-hash", "Alice", FixedNow);
        user.ConfirmEmail();
        user.UpdateProfile("Alice", "https://example.com/alice.png", FixedNow);
        var changeTime = FixedNow.AddHours(3);

        // Act
        user.ChangePassword("new-hash", changeTime);

        // Assert
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(changeTime, user.UpdatedAt);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Alice", user.Username);
        Assert.Equal("alice", user.NormalizedUsername);
        Assert.Equal("https://example.com/alice.png", user.ImageUrl);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(FixedNow, user.RegisteredAt);
    }

    [Fact]
    public void ChangeEmail_NormalizesNewEmailAndResetsConfirmationAndPreservesOtherFields()
    {
        // Arrange
        var user = User.Register("old@example.com", "hash", "Alice", FixedNow);
        user.ConfirmEmail();
        user.UpdateProfile("Alice", "https://example.com/alice.png", FixedNow);
        var changeTime = FixedNow.AddHours(4);

        // Act
        user.ChangeEmail("  New.Email@Example.COM  ", changeTime);

        // Assert
        Assert.Equal("new.email@example.com", user.Email);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(changeTime, user.UpdatedAt);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal("Alice", user.Username);
        Assert.Equal("alice", user.NormalizedUsername);
        Assert.Equal("https://example.com/alice.png", user.ImageUrl);
        Assert.Equal(FixedNow, user.RegisteredAt);
    }
}