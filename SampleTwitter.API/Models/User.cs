using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class User
{
    public const int MaxUsernameLength = 50;

    private readonly List<Repost> _reposts = new();
    private readonly List<Bookmark> _bookmarks = new();

    public long Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string? Username { get; private set; }
    public string? ImageUrl { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public IReadOnlyCollection<Repost> Reposts => _reposts;
    public IReadOnlyCollection<Bookmark> Bookmarks => _bookmarks;

    /// <summary>
    /// Used by EF Core for materialization. Application code must use <see cref="Register"/>.
    /// </summary>
    private User()
    {
    }

    /// <summary>
    /// Canonical form of an email address used for storage and lookups.
    /// </summary>
    public static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a new, not yet confirmed user account.
    /// </summary>
    public static User Register(string email, string passwordHash, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            EmailConfirmed = false,
            RegisteredAt = now ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Restarts the registration of an account whose email has not been confirmed yet.
    /// A confirmed account cannot be registered again.
    /// </summary>
    public void RenewRegistration(string passwordHash, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (EmailConfirmed)
        {
            throw new ConflictException($"User with email {Email} already exists and is confirmed.");
        }

        PasswordHash = passwordHash;
        RegisteredAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Ensures the user is allowed to sign in (credentials are verified by the caller).
    /// </summary>
    public void EnsureCanSignIn()
    {
        if (!EmailConfirmed)
        {
            throw new EmailNotConfirmedException($"User {Id} has not confirmed their email.");
        }
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
    }

    /// <summary>
    /// Replaces the profile details. The profile must still contain a username or an image afterwards.
    /// </summary>
    public void UpdateProfile(string? username, string? imageUrl, DateTimeOffset? now = null)
    {
        var normalizedUsername = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

        if (normalizedUsername is null && normalizedImageUrl is null)
        {
            throw new EmptyProfileException("User attempted to edit profile to have no username and no image.");
        }

        if (normalizedUsername is not null && normalizedUsername.Length > MaxUsernameLength)
        {
            throw new ArgumentException($"Username cannot exceed {MaxUsernameLength} characters.", nameof(username));
        }

        Username = normalizedUsername;
        ImageUrl = normalizedImageUrl;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    public void ChangePassword(string newPasswordHash, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        PasswordHash = newPasswordHash;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Switches the account to a new email address, which must be confirmed again.
    /// Uniqueness of the address is enforced by the caller / database.
    /// </summary>
    public void ChangeEmail(string newEmail, DateTimeOffset? now = null)
    {
        Email = NormalizeEmail(newEmail);
        EmailConfirmed = false;
        UpdatedAt = now ?? DateTimeOffset.UtcNow;
    }
}