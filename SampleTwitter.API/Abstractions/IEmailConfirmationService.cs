using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IEmailConfirmationService
{
    Task SendConfirmationEmail(long userId, string email, CancellationToken ct = default);
    Task<ConfirmEmailResult> ConfirmEmail(long userId, string token, CancellationToken ct = default);
}