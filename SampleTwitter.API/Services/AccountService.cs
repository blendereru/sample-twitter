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
    private readonly ApplicationContext _applicationContext;
    private readonly IEmailConfirmationService _emailConfirmationService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AccountService> _logger;
    public AccountService(ApplicationContext applicationContext, IEmailConfirmationService emailConfirmationService, 
        IPasswordHasher passwordHasher, ILogger<AccountService> logger)
    {
        _applicationContext = applicationContext;
        _emailConfirmationService = emailConfirmationService;
        _passwordHasher = passwordHasher;
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
            await _emailConfirmationService.SendConfirmationEmail(existingUser, ct);

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

        await _emailConfirmationService.SendConfirmationEmail(user, ct);

        return new RegisterResult(user.Id, user.Email, IsNewRegistration: true);
    }
    
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}