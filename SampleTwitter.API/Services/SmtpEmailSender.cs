using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SampleTwitter.API.Abstractions;
using SampleTwitter.API.Exceptions;
using SampleTwitter.API.Options;

namespace SampleTwitter.API.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task Send(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_options.SmtpUsername, _options.SmtpPassword, ct);
            await client.SendAsync(message, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Email send to {Email} was cancelled", toEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email to {Email} via {SmtpHost}:{SmtpPort}",
                toEmail, _options.SmtpHost, _options.SmtpPort);
            throw new EmailDeliveryException($"Failed to send email to {toEmail}.", ex);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, ct);
            }
        }
    }
}