using Microsoft.EntityFrameworkCore;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Infra.Repositories;

public sealed class UserRepository(TripperDbContext db) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Username == username, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task AddAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}