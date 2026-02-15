using Tripper.Application.DTOs;
using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Persistence;

public interface IItemRepository
{
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<bool> IsUserMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<List<Guid>> GetAllMemberUserIdsAsync(Guid groupId, CancellationToken ct);
    Task<int> CountMembersMatchingAsync(Guid groupId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    Task<string?> GetUsernameAsync(Guid userId, CancellationToken ct);
    Task<Dictionary<Guid, string>> GetUsernamesAsync(Guid groupId, CancellationToken ct); // for balances
    void Add(Item item);
    Task<Item?> FindByIdAsync(Guid itemId, CancellationToken ct);
    void Remove(Item item);
    Task<List<ItemResponse>> GetItemsAsync(Guid groupId, CancellationToken ct);
    Task<List<Item>> GetItemsRawAsync(Guid groupId, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<List<Item>> GetItemsForGroupAsync(Guid groupId, CancellationToken ct);
    void AddSettlementSnapshot(SettlementSnapshot snapshot);
}