using Tripper.Application.Common;
using Tripper.Application.DTOs;

namespace Tripper.Application.Interfaces.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> SignupAsync(SignupRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
}