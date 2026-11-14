using System.Text.RegularExpressions;
using SampleTwitter.API.Exceptions;

namespace SampleTwitter.API.Models;

public class User
{
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 30;
    public const string UsernameAllowedCharactersPattern = "^[a-zA-Z0-9_]+$";

    private static readonly Regex UsernameAllowedCharactersRegex =
        new(UsernameAllowedCharactersPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Handles that cannot be claimed because they collide with routes or could be used to impersonate staff.
    /// </summary>
    private static readonly HashSet<string> ReservedUsernames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "api", "auth", "bookmarks", "explore", "help", "home", "login", "logout",
        "me", "messages", "moderator", "notifications", "null", "official", "profile", "root", "search",
        "settings", "signin", "signup", "staff", "support", "system", "undefined"
    };

    private readonly List<Repost> _reposts = new();
    private readonly List<Bookmark> _bookmarks = new();
    private readonly List<Follow> _followers = new();
    private readonly List<Follow> _following = new();

    public long Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string Username { get; private set; } = null!;
    /// <summary>
    /// Case-insensitive form of <see cref="Username"/> used to enforce uniqueness across accounts.
    /// </summary>
    public string NormalizedUsername { get; private set; } = null!;
    public string? ImageUrl { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public bool EmailConfirmed { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public IReadOnlyCollection<Repost> Reposts => _reposts;
    public IReadOnlyCollection<Bookmark> Bookmarks => _bookmarks;
    public IReadOnlyCollection<Follow> Followers => _followers;
    public IReadOnlyCollection<Follow> Following => _following;

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
    /// Canonical form of a username used for case-insensitive uniqueness checks.
    /// </summary>
    public static string NormalizeUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return username.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns a human-readable reason why <paramref name="username"/> is not an acceptable handle,
    /// or <c>null</c> when it is valid. Surrounding whitespace is ignored.
    /// </summary>
    public static string? GetUsernameValidationError(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username is required.";
        }

        var trimmed = username.Trim();

        if (trimmed.Length < MinUsernameLength || trimmed.Length > MaxUsernameLength)
        {
            return $"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.";
        }

        if (!UsernameAllowedCharactersRegex.IsMatch(trimmed))
        {
            return "Username can only contain letters, digits and underscores.";
        }

        if (ReservedUsernames.Contains(trimmed))
        {
            return "This username is reserved.";
        }

        return null;
    }

    /// <summary>
    /// Creates a new, not yet confirmed user account.
    /// Uniqueness of the email and username is enforced by the caller / database.
    /// </summary>
    public static User Register(string email, string passwordHash, string username, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        var validUsername = EnsureValidUsername(username);

        return new User
        {
            Email = NormalizeEmail(email),
            PasswordHash = passwordHash,
            Username = validUsername,
            NormalizedUsername = NormalizeUsername(validUsername),
            EmailConfirmed = false,
            RegisteredAt = now ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Restarts the registration of an account whose email has not been confirmed yet.
    /// A confirmed account cannot be registered again.
    /// When <paramref name="username"/> is provided it replaces the current one; otherwise the current one is kept.
    /// </summary>
    public void RenewRegistration(string passwordHash, string? username, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        if (EmailConfirmed)
        {
            throw new ConflictException($"User with email {Email} already exists and is confirmed.");
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            var validUsername = EnsureValidUsername(username);
            Username = validUsername;
            NormalizedUsername = NormalizeUsername(validUsername);
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
    /// Replaces the profile details. A username is always required; the image is optional.
    /// Uniqueness of the username is enforced by the caller / database.
    /// </summary>
    public void UpdateProfile(string username, string? imageUrl, DateTimeOffset? now = null)
    {
        var validUsername = EnsureValidUsername(username);
        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

        Username = validUsername;
        NormalizedUsername = NormalizeUsername(validUsername);
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

    private static string EnsureValidUsername(string? username)
    {
        var error = GetUsernameValidationError(username);
        if (error is not null)
        {
            throw new InvalidUsernameException($"Username '{username}' was rejected: {error}", error);
        }

        return username!.Trim();
    }
}