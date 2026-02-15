using Microsoft.EntityFrameworkCore;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Infra.Repositories;

public sealed class ItemRepository(TripperDbContext db) : IItemRepository
{
    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<bool> IsUserMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<List<Guid>> GetAllMemberUserIdsAsync(Guid groupId, CancellationToken ct) =>
        db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => gm.UserId)
            .ToListAsync(ct);

    public Task<int> CountMembersMatchingAsync(Guid groupId, IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        db.GroupMembers.CountAsync(gm => gm.GroupId == groupId && userIds.Contains(gm.UserId), ct);

    public Task<string?> GetUsernameAsync(Guid userId, CancellationToken ct) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(ct);

    public async Task<Dictionary<Guid, string>> GetUsernamesAsync(Guid groupId, CancellationToken ct)
    {
        var rows = await db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.GroupId == groupId)
            .Select(gm => new { gm.UserId, gm.User.Username })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.UserId, x => x.Username);
    }

    public void Add(Item item) => db.Items.Add(item);

    public Task<Item?> FindByIdAsync(Guid itemId, CancellationToken ct) =>
        db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);

    public void Remove(Item item) => db.Items.Remove(item);

    public Task<List<ItemResponse>> GetItemsAsync(Guid groupId, CancellationToken ct) =>
        db.Items
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .Include(i => i.PaidByUser)
            .Select(i => new ItemResponse(
                i.Id, i.GroupId, i.Title, i.Amount, i.Currency, i.Description,
                i.PaidByMemberId, i.PaidByUser.Username, i.PayeeUserIds, i.CreatedAt
            ))
            .ToListAsync(ct);

    public Task<List<Item>> GetItemsRawAsync(Guid groupId, CancellationToken ct) =>
        db.Items
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .ToListAsync(ct);
    
    public Task<List<Item>> GetItemsForGroupAsync(Guid groupId, CancellationToken ct = default) =>
        db.Items
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .ToListAsync(ct);
    

    public void AddSettlementSnapshot(SettlementSnapshot snapshot) => db.SettlementSnapshots.Add(snapshot);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}