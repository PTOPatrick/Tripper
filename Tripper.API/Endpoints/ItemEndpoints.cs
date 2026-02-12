using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tripper.Core.DTOs;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.API.Endpoints;

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups/{groupId:guid}/items")
            .WithTags("Items")
            .RequireAuthorization();

        // Create Item
        group.MapPost("/", async (Guid groupId, CreateItemRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth check: Must be Member
            var isMember = await db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            var newItem = new Item
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                PaidByMemberId = request.PaidByMemberId ?? userId, // specific payer or self
                Amount = request.Amount,
                Currency = request.Currency,
                Title = request.Title,
                Description = request.Description ?? "",
                CreatedAt = DateTime.UtcNow,
                PayeeUserIds = request.PayeeUserIds // EF Core maps this to array/json
            };
            
            // If payees list is empty, maybe default to all members? MVP requirement: "Payees (list of MemberIds)"
            if (newItem.PayeeUserIds == null || newItem.PayeeUserIds.Count == 0)
            {
                 // Fetch all group members
                 newItem.PayeeUserIds = await db.GroupMembers
                     .Where(gm => gm.GroupId == groupId)
                     .Select(gm => gm.UserId)
                     .ToListAsync();
            }

            db.Items.Add(newItem);
            await db.SaveChangesAsync();

            return Results.Ok(new ItemResponse(newItem.Id, newItem.GroupId, newItem.Title, newItem.Amount, newItem.Currency, newItem.Description, newItem.PaidByMemberId, "", newItem.PayeeUserIds, newItem.CreatedAt));
        });

        // List Items
        group.MapGet("/", async (Guid groupId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Must be Member
            var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            var items = await db.Items
                .Where(i => i.GroupId == groupId)
                .Include(i => i.PaidByUser)
                .Select(i => new ItemResponse(
                    i.Id, i.GroupId, i.Title, i.Amount, i.Currency, i.Description, 
                    i.PaidByMemberId, i.PaidByUser.Username, i.PayeeUserIds, i.CreatedAt))
                .ToListAsync();

            return Results.Ok(items);
        });

        // Get Balances
        app.MapGet("/groups/{groupId:guid}/balances", async (Guid groupId, TripperDbContext db, ClaimsPrincipal user) =>
        {
             var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

             // Auth: Must be Member
             var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
             if (!isMember) return Results.Forbid();

             var items = await db.Items
                 .Where(i => i.GroupId == groupId)
                 .ToListAsync();
            
             var members = await db.GroupMembers
                 .Where(gm => gm.GroupId == groupId)
                 .Include(gm => gm.User)
                 .ToListAsync();

             // Calculate balances
             var balances = new Dictionary<Guid, decimal>();
             foreach(var m in members) balances[m.UserId] = 0;

             foreach(var item in items)
             {
                 // Add paid amount to payer
                 if (balances.ContainsKey(item.PaidByMemberId))
                    balances[item.PaidByMemberId] += item.Amount;
                 else 
                    balances[item.PaidByMemberId] = item.Amount; // In case payer left group?

                 // Subtract split amount from payees
                 if (item.PayeeUserIds != null && item.PayeeUserIds.Any())
                 {
                     var splitCount = item.PayeeUserIds.Count;
                     var splitAmount = item.Amount / splitCount;
                     foreach(var payeeId in item.PayeeUserIds)
                     {
                         if(balances.ContainsKey(payeeId))
                            balances[payeeId] -= splitAmount;
                     }
                 }
             }

             var result = balances.Select(kvp => new BalanceDto(
                 kvp.Key, 
                 members.FirstOrDefault(m => m.UserId == kvp.Key)?.User.Username ?? "Unknown", 
                 Math.Round(kvp.Value, 2), 
                 "USD" // Simplified currency
             )).ToList();

             return Results.Ok(result);

        }).WithTags("Items").RequireAuthorization();

        // Delete Item
        group.MapDelete("/{itemId:guid}", async (Guid groupId, Guid itemId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Member
            var member = await db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (member == null) return Results.Forbid();

            var item = await db.Items.FindAsync(itemId);
            if (item == null) return Results.NotFound();
            if (item.GroupId != groupId) return Results.NotFound();

            // Permissions: Admin or Owner
            if (member.Role != GroupRole.Admin && item.PaidByMemberId != userId)
            {
                return Results.Forbid();
            }

            db.Items.Remove(item);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}
