namespace SampleTwitter.API.Abstractions;

public interface IEmailSender
{
    Task Send(string toEmail, string subject, string htmlContent, CancellationToken ct = default);
}