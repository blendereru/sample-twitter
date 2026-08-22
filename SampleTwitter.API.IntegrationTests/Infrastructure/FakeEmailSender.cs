using System.Collections.Concurrent;
using SampleTwitter.API.Abstractions;

namespace SampleTwitter.API.IntegrationTests.Infrastructure;

public class FakeEmailSender : IEmailSender
{
    private ConcurrentBag<SentEmail> _sentEmails = [];

    public IReadOnlyList<SentEmail> SentEmails => _sentEmails.ToList();

    public Task Send(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        _sentEmails.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
    
    public void Clear() => _sentEmails = [];
}

public record SentEmail(string ToEmail, string Subject, string HtmlBody);