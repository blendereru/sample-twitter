using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IAccountService
{
    Task<RegisterResult> Register(SignUpRequest request, CancellationToken ct = default);
    Task<LoginResult> Login(LoginRequest request, CancellationToken ct = default);
    Task<UserResult> GetUserById(long id, CancellationToken ct = default);
    Task<EditProfileResult> Edit(long id, EditProfileRequest request, long currentUserId, CancellationToken ct = default);
    Task ChangePassword(long userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<ChangeEmailResult> ChangeEmail(long userId, ChangeEmailRequest request, CancellationToken ct = default);
    Task ForgotPassword(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPassword(ResetPasswordRequest request, CancellationToken ct = default);
}