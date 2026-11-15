using Microsoft.EntityFrameworkCore;
using Npgsql;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Results;
using Serilog.Context;

namespace SampleTwitter.API.Services;

public class AccountService : IAccountService
{
    private const string UsernameUniqueIndexName = "IX_Users_NormalizedUsername";
    private const int MaxUsernameGenerationAttempts = 5;

    private readonly ApplicationContext _applicationContext;
    private readonly IEmailConfirmationService _emailConfirmationService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUsernameGenerator _usernameGenerator;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        ApplicationContext applicationContext,
        IEmailConfirmationService emailConfirmationService,
        IPasswordHasher passwordHasher,
        IUsernameGenerator usernameGenerator,
        ILogger<AccountService> logger)
    {
        _applicationContext = applicationContext;
        _emailConfirmationService = emailConfirmationService;
        _passwordHasher = passwordHasher;
        _usernameGenerator = usernameGenerator;
        _logger = logger;
    }

    public async Task<RegisterResult> Register(SignUpRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        var requestedUsername = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();

        using var _ = LogContext.PushProperty("Email", normalizedEmail);

        _logger.LogInformation("Registration attempt started");

        var existingUser = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (existingUser is not null)
        {
            existingUser.RenewRegistration(_passwordHasher.Hash(request.Password), requestedUsername);

            if (requestedUsername is not null)
            {
                await EnsureUsernameIsAvailable(existingUser.NormalizedUsername, existingUser.Id, ct);
            }

            _logger.LogInformation(
                "Existing unconfirmed registration found for user {UserId} — resending confirmation",
                existingUser.Id);

            try
            {
                await _applicationContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, UsernameUniqueIndexName))
            {
                _logger.LogWarning(ex, "Race condition: duplicate username detected during registration renewal for user {UserId}", existingUser.Id);
                throw new UsernameTakenException($"Username '{requestedUsername}' is already taken (race condition).");
            }

            await _emailConfirmationService.SendConfirmationEmail(existingUser.Id, existingUser.Email, ct);

            return new RegisterResult(existingUser.Id, existingUser.Email, IsNewRegistration: false);
        }

        var username = requestedUsername ?? await GenerateUniqueUsername(ct);
        var user = User.Register(normalizedEmail, _passwordHasher.Hash(request.Password), username);

        if (requestedUsername is not null)
        {
            await EnsureUsernameIsAvailable(user.NormalizedUsername, excludedUserId: null, ct);
        }

        _applicationContext.Users.Add(user);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex, UsernameUniqueIndexName))
        {
            _logger.LogWarning(ex, "Race condition: duplicate username detected during registration");
            throw new UsernameTakenException($"Username '{username}' is already taken (race condition).");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate registration attempt detected during save");
            throw new ConflictException($"User with email {normalizedEmail} already exists (race condition).");
        }

        _logger.LogInformation(
            "New user registered with id {UserId} ({UsernameSource} username)",
            user.Id,
            requestedUsername is null ? "generated" : "chosen");

        await _emailConfirmationService.SendConfirmationEmail(user.Id, user.Email, ct);

        return new RegisterResult(user.Id, user.Email, IsNewRegistration: true);
    }

    public async Task<LoginResult> Login(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);

        using var _ = LogContext.PushProperty("Email", normalizedEmail);

        _logger.LogInformation("Login attempt started");

        var user = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
        {
            _logger.LogWarning("Login failed — user not found");
            throw new InvalidCredentialsException($"No user found with email {normalizedEmail}.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed — invalid password for user {UserId}", user.Id);
            throw new InvalidCredentialsException($"Invalid password for user {user.Id}.");
        }

        user.EnsureCanSignIn();

        _logger.LogInformation("User {UserId} authenticated successfully", user.Id);

        return new LoginResult(user.Id, user.Email);
    }

    public async Task<UserResult> GetUserById(long id, long? viewerUserId = null, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserResult(
                u.Id,
                u.Email,
                u.RegisteredAt,
                u.Username,
                u.ImageUrl,
                u.UpdatedAt,
                u.Followers.Count,
                u.Following.Count,
                viewerUserId.HasValue && u.Followers.Any(f => f.FollowerId == viewerUserId.Value)))
            .SingleOrDefaultAsync(ct);

        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found", id);
            throw new UserNotFoundException($"User with id {id} was not found.");
        }

        return user;
    }

    public async Task<EditProfileResult> Edit(long id, EditProfileRequest request, long currentUserId, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            _logger.LogWarning("Edit profile failed — user {UserId} not found", id);
            throw new UserNotFoundException($"User with id {id} was not found.");
        }

        if (user.Id != currentUserId)
        {
            _logger.LogWarning("Edit profile forbidden — user {CurrentUserId} attempted to edit profile of user {TargetUserId}", currentUserId, id);
            throw new ForbiddenException($"User {currentUserId} attempted to edit profile of user {id}.");
        }

        var normalizedUsername = User.NormalizeUsername(request.Username);

        var usernameTaken = await _applicationContext.Users
            .AnyAsync(u => u.Id != id && u.NormalizedUsername == normalizedUsername, ct);

        if (usernameTaken)
        {
            _logger.LogWarning("Edit profile failed — username '{Username}' is already taken", request.Username);
            throw new UsernameTakenException($"Username '{request.Username}' is already taken.");
        }

        user.UpdateProfile(request.Username, request.ImageUrl);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate username detected during edit profile for user {UserId}", id);
            throw new UsernameTakenException($"Username '{request.Username}' is already taken (race condition).");
        }

        _logger.LogInformation("User {UserId} profile edited", user.Id);

        return new EditProfileResult(
            user.Id,
            user.Username,
            user.ImageUrl,
            user.UpdatedAt);
    }

    public async Task ChangePassword(long userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            _logger.LogWarning("Change password failed — user {UserId} not found", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Change password failed — invalid current password for user {UserId}", userId);
            throw new InvalidCredentialsException($"Invalid current password for user {userId}.");
        }

        var unusedResetTokens = await _applicationContext.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync(ct);

        if (unusedResetTokens.Count > 0)
        {
            _applicationContext.PasswordResetTokens.RemoveRange(unusedResetTokens);
        }

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} changed password", userId);
    }

    public async Task<ChangeEmailResult> ChangeEmail(long userId, ChangeEmailRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.NewEmail);

        var user = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            _logger.LogWarning("Change email failed — user {UserId} not found", userId);
            throw new UserNotFoundException($"User with id {userId} was not found.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Change email failed — invalid current password for user {UserId}", userId);
            throw new InvalidCredentialsException($"Invalid current password for user {userId}.");
        }

        var emailExists = await _applicationContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, ct);

        if (emailExists)
        {
            _logger.LogWarning("Change email rejected — email {Email} already in use", normalizedEmail);
            throw new ConflictException($"User with email {normalizedEmail} already exists.");
        }

        user.ChangeEmail(normalizedEmail);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate email detected during change email for user {UserId}", userId);
            throw new ConflictException($"User with email {normalizedEmail} already exists (race condition).");
        }

        _logger.LogInformation("User {UserId} changed email — sending confirmation to new address", userId);

        await _emailConfirmationService.SendConfirmationEmail(user.Id, user.Email, ct);

        return new ChangeEmailResult(user.Id, user.Email, user.UpdatedAt);
    }

    public async Task<FollowResult> Follow(long followId, long currentUserId, CancellationToken ct = default)
    {
        var targetExists = await _applicationContext.Users
            .AnyAsync(u => u.Id == followId, ct);

        if (!targetExists)
        {
            _logger.LogWarning("Follow failed — target user {TargetUserId} not found", followId);
            throw new UserNotFoundException($"User with id {followId} was not found.");
        }

        var followerExists = await _applicationContext.Users
            .AnyAsync(u => u.Id == currentUserId, ct);

        if (!followerExists)
        {
            _logger.LogWarning("Follow failed — follower user {FollowerUserId} not found", currentUserId);
            throw new UserNotFoundException($"User with id {currentUserId} was not found.");
        }

        var follow = Models.Follow.Create(currentUserId, followId);

        var alreadyFollowing = await _applicationContext.Follows
            .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == followId, ct);

        if (alreadyFollowing)
        {
            _logger.LogWarning("Follow failed — user {FollowerId} is already following user {FollowingId}", currentUserId, followId);
            throw new AlreadyFollowedException($"User {currentUserId} is already following user {followId}.");
        }

        _applicationContext.Follows.Add(follow);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate follow attempt detected during save from user {FollowerId} to user {FollowingId}", currentUserId, followId);
            throw new AlreadyFollowedException($"User {currentUserId} is already following user {followId} (race condition).");
        }

        _logger.LogInformation("User {FollowerId} followed user {FollowingId}", currentUserId, followId);

        return new FollowResult(follow.FollowerId, follow.FollowingId, follow.CreatedAt);
    }

    public async Task Unfollow(long followId, long currentUserId, CancellationToken ct = default)
    {
        var follow = await _applicationContext.Follows
            .SingleOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == followId, ct);

        if (follow is null)
        {
            _logger.LogWarning(
                "Unfollow failed — user {FollowerId} is not following user {FollowingId} or user not found",
                currentUserId,
                followId);
            throw new FollowNotFoundException($"Follow relationship from user {currentUserId} to user {followId} was not found.");
        }

        _applicationContext.Follows.Remove(follow);
        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {FollowerId} unfollowed user {FollowingId}", currentUserId, followId);
    }

    private async Task EnsureUsernameIsAvailable(string normalizedUsername, long? excludedUserId, CancellationToken ct)
    {
        var usernameTaken = await _applicationContext.Users
            .AnyAsync(u => u.NormalizedUsername == normalizedUsername && (excludedUserId == null || u.Id != excludedUserId), ct);

        if (usernameTaken)
        {
            _logger.LogWarning("Registration failed — username '{NormalizedUsername}' is already taken", normalizedUsername);
            throw new UsernameTakenException($"Username '{normalizedUsername}' is already taken.");
        }
    }

    private async Task<string> GenerateUniqueUsername(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxUsernameGenerationAttempts; attempt++)
        {
            var candidate = _usernameGenerator.Generate();
            var normalizedCandidate = User.NormalizeUsername(candidate);

            var taken = await _applicationContext.Users
                .AnyAsync(u => u.NormalizedUsername == normalizedCandidate, ct);

            if (!taken)
            {
                return candidate;
            }

            _logger.LogWarning("Generated username collided with an existing account (attempt {Attempt})", attempt);
        }

        throw new InvalidOperationException(
            $"Failed to generate a unique username after {MaxUsernameGenerationAttempts} attempts.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex, string? constraintName = null)
    {
        return ex.InnerException is PostgresException pgEx
               && pgEx.SqlState == PostgresErrorCodes.UniqueViolation
               && (constraintName is null || pgEx.ConstraintName == constraintName);
    }
}