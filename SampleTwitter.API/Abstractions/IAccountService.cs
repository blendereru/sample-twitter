using SampleTwitter.API.DTOs.RequestDTOs;
using SampleTwitter.API.Results;

namespace SampleTwitter.API.Abstractions;

public interface IAccountService
{
    Task<RegisterResult> Register(SignUpRequest request, CancellationToken ct = default);
    Task<LoginResult> Login(LoginRequest request, CancellationToken ct = default);
}