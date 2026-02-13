using Microsoft.EntityFrameworkCore;
using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Application.Services;

public sealed class ItemService(TripperDbContext db) : IItemService
{
    public async Task<Result<ItemResponse>> CreateAsync(Guid groupId, Guid currentUserId, CreateItemRequest request, CancellationToken ct = default)
    {
        // Must be member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.title.required", "Title is required."));

        if (request.Amount <= 0)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.amount.invalid", "Amount must be greater than 0."));

        // Determine payer (defaults to current user)
        var paidByUserId = request.PaidByMemberId ?? currentUserId;

        // Validate payer is in group
        var payerInGroup = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == paidByUserId, ct);
        if (!payerInGroup)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.payer.invalid", "Payer must be a member of the group."));

        // Payees default: all members
        var payees = request.PayeeUserIds?.Distinct().ToList() ?? new List<Guid>();
        if (payees.Count == 0)
        {
            payees = await db.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Select(gm => gm.UserId)
                .ToListAsync(ct);
        }
        else
        {
            // Validate payees all in group
            var validCount = await db.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId && payees.Contains(gm.UserId), ct);

            if (validCount != payees.Count)
                return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.payees.invalid", "All payees must be members of the group."));
        }

        var now = DateTimeOffset.UtcNow;

        var item = new Item
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PaidByMemberId = paidByUserId,
            Amount = request.Amount,
            Currency = request.Currency,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedAt = now.UtcDateTime,
            PayeeUserIds = payees
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(ct);

        // Fetch payer username (for response)
        var payerUsername = await db.Users
            .Where(u => u.Id == paidByUserId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(ct) ?? "";

        return Result<ItemResponse>.Ok(new ItemResponse(
            item.Id, item.GroupId, item.Title, item.Amount, item.Currency, item.Description,
            item.PaidByMemberId, payerUsername, item.PayeeUserIds, item.CreatedAt
        ));
    }

    public async Task<Result<List<ItemResponse>>> GetAllAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        // Must be member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result<List<ItemResponse>>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var items = await db.Items
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .Include(i => i.PaidByUser)
            .Select(i => new ItemResponse(
                i.Id, i.GroupId, i.Title, i.Amount, i.Currency, i.Description,
                i.PaidByMemberId, i.PaidByUser.Username, i.PayeeUserIds, i.CreatedAt
            ))
            .ToListAsync(ct);

        return Result<List<ItemResponse>>.Ok(items);
    }

    public async Task<Result<List<BalanceDto>>> GetBalancesAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        // Must be member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result<List<BalanceDto>>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        // Load members (with usernames) and items
        var members = await db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.GroupId == groupId)
            .Include(gm => gm.User)
            .ToListAsync(ct);

        var items = await db.Items
            .AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .ToListAsync(ct);

        var balances = new Dictionary<Guid, decimal>(capacity: members.Count);
        foreach (var m in members) balances[m.UserId] = 0m;

        foreach (var item in items)
        {
            // payer gets +amount
            if (!balances.TryAdd(item.PaidByMemberId, item.Amount))
                balances[item.PaidByMemberId] += item.Amount;

            // payees get -split
            if (item.PayeeUserIds.Count == 0) continue;

            var splitAmount = item.Amount / item.PayeeUserIds.Count;

            foreach (var payeeId in item.PayeeUserIds.Where(payeeId => balances.ContainsKey(payeeId)))
            {
                balances[payeeId] -= splitAmount;
            }
        }

        // Currency note: you currently hardcode CHF. This keeps parity with your endpoint.
        var result = balances
            .Select(kvp => new BalanceDto(
                kvp.Key,
                members.FirstOrDefault(m => m.UserId == kvp.Key)?.User.Username ?? "Unknown",
                Math.Round(kvp.Value, 2),
                "CHF"
            ))
            .ToList();

        return Result<List<BalanceDto>>.Ok(result);
    }

    public async Task<Result> DeleteAsync(Guid groupId, Guid itemId, Guid currentUserId, CancellationToken ct = default)
    {
        // Must be member (and we need role)
        var member = await db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);

        if (member is null)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null || item.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "item.not_found", "Item not found."));

        // Admin or owner
        if (member.Role != GroupRole.Admin && item.PaidByMemberId != currentUserId)
            return Result.Fail(new Error(ErrorType.Forbidden, "item.forbidden", "You are not allowed to delete this item."));

        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);

        return Result.Ok();
    }
}