using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}