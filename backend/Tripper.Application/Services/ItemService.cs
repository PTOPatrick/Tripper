using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Application.Interfaces.Services;
using Tripper.Application.Interfaces.Common;
using Tripper.Core.Entities;

namespace Tripper.Application.Services;

public sealed class ItemService(IItemRepository repo, ICurrencyRateProvider rates) : IItemService
{
    private const string BaseCurrency = "CHF";

    public async Task<Result<ItemResponse>> CreateAsync(Guid groupId, Guid currentUserId, CreateItemRequest request, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result<ItemResponse>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.title.required", "Title is required."));

        if (request.Amount <= 0)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.amount.invalid", "Amount must be greater than 0."));

        var paidByUserId = request.PaidByMemberId ?? currentUserId;

        if (!await repo.IsUserMemberAsync(groupId, paidByUserId, ct))
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.payer.invalid", "Payer must be a member of the group."));

        if (request.PayeeUserIds is null)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.payees.required", "Payees are required."));

        var payees = request.PayeeUserIds.Distinct().ToList();
        var validCount = await repo.CountMembersMatchingAsync(groupId, payees, ct);
        if (validCount != payees.Count)
            return Result<ItemResponse>.Fail(new Error(ErrorType.Validation, "item.payees.invalid", "All payees must be members of the group."));

        var now = DateTimeOffset.UtcNow;

        var item = new Item
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PaidByMemberId = paidByUserId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            Description = (request.Description ?? string.Empty).Trim(),
            CreatedAt = now.UtcDateTime,
            PayeeUserIds = payees
        };

        repo.Add(item);
        await repo.SaveChangesAsync(ct);

        var payerUsername = await repo.GetUsernameAsync(paidByUserId, ct) ?? "";

        return Result<ItemResponse>.Ok(new ItemResponse(
            item.Id, item.GroupId, item.Title, item.Amount, item.Currency, item.Description,
            item.PaidByMemberId, payerUsername, item.PayeeUserIds, item.CreatedAt
        ));
    }

    public async Task<Result<List<ItemResponse>>> GetAllAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result<List<ItemResponse>>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var items = await repo.GetItemsAsync(groupId, ct);
        return Result<List<ItemResponse>>.Ok(items);
    }

    public async Task<Result<List<BalanceDto>>> GetBalancesAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result<List<BalanceDto>>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var usernames = await repo.GetUsernamesAsync(groupId, ct); // userId -> username
        var items = await repo.GetItemsRawAsync(groupId, ct);

        var balances = new Dictionary<Guid, decimal>(capacity: usernames.Count);
        foreach (var kvp in usernames) balances[kvp.Key] = 0m;

        foreach (var item in items)
        {
            // Convert item amount into CHF
            var fromCur = NormalizeCurrency(item.Currency);
            var rate = fromCur == BaseCurrency ? 1m : await rates.GetRateAsync(fromCur, BaseCurrency, ct);
            var amountChf = RoundMoney(item.Amount * rate);

            // payer gets +amount (CHF)
            if (!balances.TryAdd(item.PaidByMemberId, amountChf))
                balances[item.PaidByMemberId] = RoundMoney(balances[item.PaidByMemberId] + amountChf);

            // split among payees (CHF)
            if (item.PayeeUserIds.Count == 0) continue;

            var splitAmount = RoundMoney(amountChf / item.PayeeUserIds.Count);

            foreach (var payeeId in item.PayeeUserIds.Where(balances.ContainsKey))
                balances[payeeId] = RoundMoney(balances[payeeId] - splitAmount);
        }

        var result = balances
            .Select(kvp => new BalanceDto(
                kvp.Key,
                usernames.GetValueOrDefault(kvp.Key, "Unknown"),
                RoundMoney(kvp.Value),
                BaseCurrency
            ))
            .ToList();

        return Result<List<BalanceDto>>.Ok(result);
    }

    public async Task<Result> DeleteAsync(Guid groupId, Guid itemId, Guid currentUserId, CancellationToken ct = default)
    {
        var member = await repo.GetMembershipAsync(groupId, currentUserId, ct);
        if (member is null)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var item = await repo.FindByIdAsync(itemId, ct);
        if (item is null || item.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "item.not_found", "Item not found."));

        if (member.Role != GroupRole.Admin && item.PaidByMemberId != currentUserId)
            return Result.Fail(new Error(ErrorType.Forbidden, "item.forbidden", "You are not allowed to delete this item."));

        repo.Remove(item);
        await repo.SaveChangesAsync(ct);

        return Result.Ok();
    }

    private static string NormalizeCurrency(string? value)
    {
        var c = (value ?? BaseCurrency).Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(c) ? BaseCurrency : c;
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}