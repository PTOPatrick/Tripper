using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Persistence;

public interface IUserLookupRepository
{
    Task<User?> FindByEmailOrUsernameAsync(string emailOrUsername, CancellationToken ct);
    Task<Dictionary<Guid, string>> GetUsernamesAsync(IEnumerable<Guid> userIds, CancellationToken ct);
}