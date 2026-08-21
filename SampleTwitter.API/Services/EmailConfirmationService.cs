using Microsoft.EntityFrameworkCore;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Data;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Models;

namespace SampleTwitter.API.Services;

public class EmailConfirmationService : IEmailConfirmationService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailConfirmationService> _logger;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);
    
    public EmailConfirmationService(ApplicationContext applicationContext, ISecureTokenGenerator tokenGenerator,
        IEmailSender emailSender, IConfiguration configuration, ILogger<EmailConfirmationService> logger)
    {
        _applicationContext = applicationContext;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task SendConfirmationEmail(User user, CancellationToken ct = default)
    {
        var oldTokens = await _applicationContext.EmailConfirmationTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(ct);

        if (oldTokens.Count > 0)
        {
            _logger.LogInformation(
                "Invalidating {Count} outstanding confirmation token(s) for user {UserId}",
                oldTokens.Count, user.Id);
        }

        _applicationContext.EmailConfirmationTokens.RemoveRange(oldTokens);

        var rawToken = _tokenGenerator.Generate();
        var tokenEntity = new EmailConfirmationToken
        {
            UserId = user.Id,
            TokenHash = _tokenGenerator.Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _applicationContext.EmailConfirmationTokens.Add(tokenEntity);
        await _applicationContext.SaveChangesAsync(ct);

        var baseUrl = _configuration["AppSettings:BaseUrl"];
        var confirmationLink =
            $"{baseUrl}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(rawToken)}";

        var body = $"""
                    <p>Welcome! Please confirm your email address by clicking the link below:</p>
                    <p><a href="{confirmationLink}">Confirm my email</a></p>
                    <p>This link expires in 24 hours.</p>
                    """;

        try
        {
            await _emailSender.Send(user.Email, "Confirm your email address", body, ct);
            _logger.LogInformation("Confirmation email sent for user {UserId}", user.Id);
        }
        catch (EmailDeliveryException ex)
        {
            _logger.LogError(ex, "Failed to deliver confirmation email for user {UserId}", user.Id);
            throw;
        }
    }

    public async Task<User> ConfirmEmail(long userId, string token, CancellationToken ct = default)
    {
        var tokenHash = _tokenGenerator.Hash(token);

        var tokenEntity = await _applicationContext.EmailConfirmationTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.UserId == userId && t.TokenHash == tokenHash, ct);

        if (tokenEntity is null || tokenEntity.UsedAt is not null || tokenEntity.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Invalid or expired confirmation attempt for user {UserId}", userId);
            throw new InvalidTokenException("This confirmation link is invalid or has expired.");
        }

        tokenEntity.User.EmailConfirmed = true;
        tokenEntity.UsedAt = DateTimeOffset.UtcNow;

        await _applicationContext.SaveChangesAsync(ct);

        _logger.LogInformation("Email confirmed for user {UserId}", userId);

        return tokenEntity.User;
    }
}