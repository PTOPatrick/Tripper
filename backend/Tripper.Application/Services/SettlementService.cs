using Tripper.Application.DTOs;
using Tripper.Application.Interfaces.Common;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Application.Interfaces.Services;
using Tripper.Core.Entities;

namespace Tripper.Application.Services;

public class SettlementService(IItemRepository items, IUserLookupRepository users, ICurrencyRateProvider rates) : ISettlementService
{
    private const string BaseCurrency = "CHF";

    public async Task<SettlementDtos.SettlementSnapshotDto> RecalculateAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        // 1) Load items of group
        var groupItems = await items.GetItemsForGroupAsync(groupId, ct);

        // 2) Determine involved users (payer + payees)
        var userIds = new HashSet<Guid>();
        foreach (var it in groupItems)
        {
            userIds.Add(it.PaidByMemberId);

            foreach (var p in it.PayeeUserIds)
                userIds.Add(p);
        }

        // Falls noch keine Items existieren: Snapshot mit 0 Transfers ist ok (oder man variiert den API-Endpoint, z.B. mit /settlement)
        var usernameMap = await users.GetUsernamesAsync(userIds, ct); // Dictionary<Guid,string>

        // 3) Compute net balances in CHF
        var net = userIds.ToDictionary(id => id, _ => 0m);

        foreach (var it in groupItems)
        {
            if (it.PayeeUserIds.Count == 0)
                continue; // backend sollte das verhindern, aber safety first

            var fromCur = it.Currency.Trim().ToUpperInvariant();
            var rate = fromCur == BaseCurrency ? 1m : await rates.GetRateAsync(fromCur, BaseCurrency, ct);

            var amountChf = RoundMoney(it.Amount * rate);

            // payer gets +amount
            net[it.PaidByMemberId] = RoundMoney(net[it.PaidByMemberId] + amountChf);

            // payees share equally
            var share = RoundMoney(amountChf / it.PayeeUserIds.Count);

            foreach (var payee in it.PayeeUserIds)
                net[payee] = RoundMoney(net[payee] - share);
        }

        // 4) Create transfers (who owes whom)
        var transfers = ComputeTransfers(net);

        // 5) Persist snapshot
        var snapshot = new SettlementSnapshot
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            BaseCurrency = BaseCurrency,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId,
            RatesAsOfUtc = DateTime.UtcNow,
            ItemsIncludedCount = groupItems.Count,
            Transfers = transfers.Select(t => new SettlementTransfer
            {
                Id = Guid.NewGuid(),
                FromUserId = t.FromUserId,
                ToUserId = t.ToUserId,
                Amount = t.Amount
            }).ToList()
        };

        items.AddSettlementSnapshot(snapshot);
        await items.SaveChangesAsync(ct);

        // 6) Return DTO
        var dtoTransfers = transfers.Select(t => new SettlementDtos.SettlementTransferDto(
            t.FromUserId,
            usernameMap.GetValueOrDefault(t.FromUserId, ""),
            t.ToUserId,
            usernameMap.GetValueOrDefault(t.ToUserId, ""),
            t.Amount,
            BaseCurrency
        )).ToList();

        return new SettlementDtos.SettlementSnapshotDto(
            snapshot.Id,
            snapshot.GroupId,
            snapshot.BaseCurrency,
            snapshot.CreatedAt,
            snapshot.CreatedByUserId,
            dtoTransfers
        );
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record Transfer(Guid FromUserId, Guid ToUserId, decimal Amount);

    private static List<Transfer> ComputeTransfers(Dictionary<Guid, decimal> net)
    {
        var creditors = net.Where(kv => kv.Value > 0.005m)
            .Select(kv => (UserId: kv.Key, Amount: kv.Value))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var debtors = net.Where(kv => kv.Value < -0.005m)
            .Select(kv => (UserId: kv.Key, Amount: -kv.Value)) // positive debt
            .OrderByDescending(x => x.Amount)
            .ToList();

        var result = new List<Transfer>();

        int i = 0, j = 0;
        while (i < debtors.Count && j < creditors.Count)
        {
            var d = debtors[i];
            var c = creditors[j];

            var pay = Math.Min(d.Amount, c.Amount);
            pay = Math.Round(pay, 2, MidpointRounding.AwayFromZero);

            if (pay > 0)
                result.Add(new Transfer(d.UserId, c.UserId, pay));

            d.Amount -= pay;
            c.Amount -= pay;

            debtors[i] = d;
            creditors[j] = c;

            if (d.Amount <= 0.005m) i++;
            if (c.Amount <= 0.005m) j++;
        }

        return result;
    }
}