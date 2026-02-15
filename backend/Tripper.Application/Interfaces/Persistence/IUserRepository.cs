using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Persistence;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}