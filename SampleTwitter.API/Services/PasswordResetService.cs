using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Options;
using Serilog.Context;

namespace SampleTwitter.API.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _options;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        ApplicationContext applicationContext,
        ISecureTokenGenerator tokenGenerator,
        IPasswordHasher passwordHasher,
        IEmailSender emailSender,
        IOptions<AppOptions> options,
        ILogger<PasswordResetService> logger)
    {
        _applicationContext = applicationContext;
        _tokenGenerator = tokenGenerator;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ForgotPassword(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);

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
        var tokenEntity = PasswordResetToken.Create(user.Id, _tokenGenerator.Hash(rawToken));

        _applicationContext.PasswordResetTokens.Add(tokenEntity);
        await _applicationContext.SaveChangesAsync(ct);

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

        if (tokenEntity is null || !tokenEntity.IsValid())
        {
            _logger.LogWarning("Invalid or expired password reset attempt for user {UserId}", request.UserId);
            throw new InvalidTokenException("This password reset link is invalid or has expired.");
        }

        tokenEntity.MarkUsed();
        tokenEntity.User.ChangePassword(_passwordHasher.Hash(request.NewPassword));

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset completed for user {UserId}", request.UserId);
    }
}