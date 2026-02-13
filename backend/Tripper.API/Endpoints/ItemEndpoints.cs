using System.Security.Claims;
using Tripper.API.Common;              // ToHttpResult()
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;

namespace Tripper.API.Endpoints;

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups/{groupId:guid}/items")
            .WithTags("Items")
            .RequireAuthorization();

        group.MapPost("/", async (Guid groupId, CreateItemRequest request, IItemService itemService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await itemService.CreateAsync(groupId, userId, request, ct);
            return result.ToHttpResult();
        });

        group.MapGet("/", async (Guid groupId, IItemService itemService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await itemService.GetAllAsync(groupId, userId, ct);
            return result.ToHttpResult();
        });

        app.MapGet("/groups/{groupId:guid}/balances", async (Guid groupId, IItemService itemService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await itemService.GetBalancesAsync(groupId, userId, ct);
            return result.ToHttpResult();
        })
        .WithTags("Items")
        .RequireAuthorization();

        group.MapDelete("/{itemId:guid}", async (Guid groupId, Guid itemId, IItemService itemService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await itemService.DeleteAsync(groupId, itemId, userId, ct);
            return result.ToHttpResult();
        });
    }
}