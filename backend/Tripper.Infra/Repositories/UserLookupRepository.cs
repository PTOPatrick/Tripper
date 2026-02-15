using Microsoft.EntityFrameworkCore;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Infra.Repositories;

public sealed class UserLookupRepository(TripperDbContext db) : IUserLookupRepository
{
    public Task<User?> FindByEmailOrUsernameAsync(string emailOrUsername, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u =>
            u.Email == emailOrUsername || u.Username == emailOrUsername, ct);
    
    
    public async Task<Dictionary<Guid, string>> GetUsernamesAsync(
        IEnumerable<Guid>? userIds,
        CancellationToken ct = default)
    {
        if (userIds is null) return new Dictionary<Guid, string>();

        var ids = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return new Dictionary<Guid, string>();

        // Hinweis: EF übersetzt Contains(List<Guid>) sauber in SQL IN (...)
        return await db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToDictionaryAsync(x => x.Id, x => x.Username, ct);
    }
}