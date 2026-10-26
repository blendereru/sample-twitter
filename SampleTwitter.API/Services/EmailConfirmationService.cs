using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;
using SampleTwitter.API.Options;
using SampleTwitter.API.Results;
using Serilog.Context;

namespace SampleTwitter.API.Services;

public class EmailConfirmationService : IEmailConfirmationService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly AppOptions _options;
    private readonly ILogger<EmailConfirmationService> _logger;

    public EmailConfirmationService(ApplicationContext applicationContext, ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender, IOptions<AppOptions> options, ILogger<EmailConfirmationService> logger)
    {
        _applicationContext = applicationContext;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public Task SendConfirmationEmail(User user, CancellationToken ct = default)
        => SendConfirmationEmail(user.Id, user.Email, ct);

    public async Task SendConfirmationEmail(long userId, string email, CancellationToken ct = default)
    {
        var oldTokens = await _applicationContext.EmailConfirmationTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync(ct);

        if (oldTokens.Count > 0)
        {
            _logger.LogInformation(
                "Invalidating {Count} outstanding confirmation token(s) for user {UserId}",
                oldTokens.Count, userId);
        }

        _applicationContext.EmailConfirmationTokens.RemoveRange(oldTokens);

        var rawToken = _tokenGenerator.Generate();
        var tokenEntity = EmailConfirmationToken.Create(userId, _tokenGenerator.Hash(rawToken));

        _applicationContext.EmailConfirmationTokens.Add(tokenEntity);
        await _applicationContext.SaveChangesAsync(ct);

        var baseUrl = _options.BaseUrl;
        var confirmationLink =
            $"{baseUrl}/confirm-email?userId={userId}&token={Uri.EscapeDataString(rawToken)}";

        var body = $"""
                    <p>Welcome! Please confirm your email address by clicking the link below:</p>
                    <p><a href="{confirmationLink}">Confirm my email</a></p>
                    <p>This link expires in 24 hours.</p>
                    """;

        try
        {
            await _emailSender.Send(email, "Confirm your email address", body, ct);
            _logger.LogInformation("Confirmation email sent for user {UserId}", userId);
        }
        catch (EmailDeliveryException ex)
        {
            _logger.LogError(ex, "Failed to deliver confirmation email for user {UserId}", userId);
            throw;
        }
    }

    public async Task ResendConfirmationEmail(ResendConfirmationEmailRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);

        using var _ = LogContext.PushProperty("Email", normalizedEmail);

        var user = await _applicationContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || user.EmailConfirmed)
        {
            _logger.LogInformation("Resend confirmation email requested for non-existent or already-confirmed account");
            return;
        }

        await SendConfirmationEmail(user.Id, user.Email, ct);
    }

    public async Task<ConfirmEmailResult> ConfirmEmail(long userId, string token, CancellationToken ct = default)
    {
        var tokenHash = _tokenGenerator.Hash(token);

        var tokenEntity = await _applicationContext.EmailConfirmationTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.UserId == userId && t.TokenHash == tokenHash, ct);

        if (tokenEntity is null || !tokenEntity.IsValid())
        {
            _logger.LogWarning("Invalid or expired confirmation attempt for user {UserId}", userId);
            throw new InvalidTokenException("This confirmation link is invalid or has expired.");
        }

        tokenEntity.MarkUsed();
        tokenEntity.User.ConfirmEmail();

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Email confirmed for user {UserId}", userId);

        return new ConfirmEmailResult(tokenEntity.User.Id, tokenEntity.User.Email);
    }
}