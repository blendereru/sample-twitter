using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Options;
using SampleTwitter.API.Results;
using Serilog.Context;

namespace SampleTwitter.API.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationContext _applicationContext;
    private readonly IEmailConfirmationService _emailConfirmationService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _options;
    private readonly ILogger<AccountService> _logger;
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    public AccountService(
        ApplicationContext applicationContext,
        IEmailConfirmationService emailConfirmationService,
        IPasswordHasher passwordHasher,
        ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender,
        IOptions<AppOptions> options,
        ILogger<AccountService> logger)
    {
        _applicationContext = applicationContext;
        _emailConfirmationService = emailConfirmationService;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RegisterResult> Register(SignUpRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        using var _ = LogContext.PushProperty("Email", normalizedEmail);

        _logger.LogInformation("Registration attempt started");

        var existingUser = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (existingUser is not null)
        {
            if (existingUser.EmailConfirmed)
            {
                _logger.LogWarning("Registration rejected — email already confirmed");
                throw new ConflictException($"User with email {normalizedEmail} already exists and is confirmed.");
            }

            _logger.LogInformation(
                "Existing unconfirmed registration found for user {UserId} — resending confirmation",
                existingUser.Id);

            existingUser.RegisteredAt = DateTimeOffset.UtcNow;
            existingUser.PasswordHash = _passwordHasher.Hash(request.Password);
            await _applicationContext.SaveChangesAsync(ct);
            await _emailConfirmationService.SendConfirmationEmail(existingUser.Id, existingUser.Email, ct);

            return new RegisterResult(existingUser.Id, existingUser.Email, IsNewRegistration: false);
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            EmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        _applicationContext.Users.Add(user);

        try
        {
            await _applicationContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Race condition: duplicate registration attempt detected during save");
            throw new ConflictException($"User with email {normalizedEmail} already exists (race condition).");
        }

        _logger.LogInformation("New user registered with id {UserId}", user.Id);

        await _emailConfirmationService.SendConfirmationEmail(user.Id, user.Email, ct);

        return new RegisterResult(user.Id, user.Email, IsNewRegistration: true);
    }

    public async Task<LoginResult> Login(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

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

        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login failed — email not confirmed for user {UserId}", user.Id);
            throw new EmailNotConfirmedException($"User {user.Id} has not confirmed their email.");
        }

        _logger.LogInformation("User {UserId} authenticated successfully", user.Id);

        return new LoginResult(user.Id, user.Email);
    }

    public async Task<UserResult> GetUserById(long id, CancellationToken ct = default)
    {
        var user = await _applicationContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found", id);
            throw new UserNotFoundException($"User with id {id} was not found.");
        }

        return new UserResult(user.Id, user.Email, user.RegisteredAt, user.Username, user.ImageUrl, user.UpdatedAt);
    }

    public async Task<EditProfileResult> Edit(long id, EditProfileRequest request, long currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) && string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            throw new EmptyProfileException("User attempted to edit profile to have no username and no image.");
        }

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

        user.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        user.ImageUrl = request.ImageUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

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

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} changed password", userId);
    }

    public async Task<ChangeEmailResult> ChangeEmail(long userId, ChangeEmailRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.NewEmail.Trim().ToLowerInvariant();

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

        user.Email = normalizedEmail;
        user.EmailConfirmed = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;

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

    public async Task ForgotPassword(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        using var _ = LogContext.PushProperty("Email", normalizedEmail);

        var user = await _applicationContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.EmailConfirmed)
        {
            _logger.LogInformation("Forgot password requested for non-existent or unconfirmed account");
            return;
        }

        var oldTokens = await _applicationContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(ct);

        if (oldTokens.Count > 0)
        {
            _logger.LogInformation(
                "Invalidating {Count} outstanding password reset token(s) for user {UserId}",
                oldTokens.Count, user.Id);
            _applicationContext.PasswordResetTokens.RemoveRange(oldTokens);
        }

        var rawToken = _tokenGenerator.Generate();
        var tokenEntity = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _tokenGenerator.Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(PasswordResetTokenLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _applicationContext.PasswordResetTokens.Add(tokenEntity);
        await _applicationContext.SaveChangesAsync(ct);

        // TODO: separate as a service
        var baseUrl = _options.BaseUrl;
        var resetLink =
            $"{baseUrl}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(rawToken)}";

        var body = $"""
                    <p>We received a request to reset your password. Click the link below to set a new password:</p>
                    <p><a href="{resetLink}">Reset my password</a></p>
                    <p>This link expires in 1 hour.</p>
                    """;

        try
        {
            await _emailSender.Send(user.Email, "Reset your password", body, ct);
            _logger.LogInformation("Password reset email sent for user {UserId}", user.Id);
        }
        catch (EmailDeliveryException ex)
        {
            _logger.LogError(ex, "Failed to deliver password reset email for user {UserId}", user.Id);
            throw;
        }
    }

    public async Task ResetPassword(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var tokenHash = _tokenGenerator.Hash(request.Token);

        var tokenEntity = await _applicationContext.PasswordResetTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.UserId == request.UserId && t.TokenHash == tokenHash, ct);

        if (tokenEntity is null || tokenEntity.UsedAt is not null || tokenEntity.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Invalid or expired password reset attempt for user {UserId}", request.UserId);
            throw new InvalidTokenException("This password reset link is invalid or has expired.");
        }

        tokenEntity.User.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        tokenEntity.User.UpdatedAt = DateTimeOffset.UtcNow;
        tokenEntity.UsedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset completed for user {UserId}", request.UserId);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}