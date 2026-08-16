using SampleTwitter.API.Abstractions;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

public class FakeEmailSender : IEmailSender
{
    private readonly List<SentEmail> _sentEmails = [];

    public IReadOnlyList<SentEmail> SentEmails => _sentEmails;

    public Task Send(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _sentEmails.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }

    public void Clear() => _sentEmails.Clear();
}

public record SentEmail(string ToEmail, string Subject, string HtmlBody);