using SampleTwitter.API.Models;

namespace SampleTwitter.API.Abstractions;

public interface IEmailConfirmationService
{
    Task SendConfirmationEmail(User user, CancellationToken ct = default);
    Task<User> ConfirmEmail(long userId, string token, CancellationToken ct = default);
}