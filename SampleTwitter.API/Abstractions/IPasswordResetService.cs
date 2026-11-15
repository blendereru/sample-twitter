using SampleTwitter.API.DTOs.RequestDTOs;

namespace SampleTwitter.API.Abstractions;

public interface IPasswordResetService
{
    Task ForgotPassword(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPassword(ResetPasswordRequest request, CancellationToken ct = default);
}