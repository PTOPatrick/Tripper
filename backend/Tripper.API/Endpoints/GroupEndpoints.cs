using System.Security.Claims;
using Tripper.API.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Services;
using Tripper.Application.Services;

namespace Tripper.API.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups").WithTags("Groups").RequireAuthorization();

        group.MapPost("/", async (CreateGroupRequest request, IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.CreateAsync(userId, request, ct);
            return result.ToHttpResult();
        });

        group.MapGet("/", async (IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.GetMyGroupsAsync(userId, ct);
            return result.ToHttpResult();
        });

        group.MapGet("/{groupId:guid}", async (Guid groupId, IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.GetDetailsAsync(userId, groupId, ct);
            return result.ToHttpResult();
        });

        group.MapPost("/{groupId:guid}/members", async (Guid groupId, AddMemberRequest request, IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.AddMemberAsync(userId, groupId, request, ct);
            return result.ToHttpResult();
        });

        group.MapPatch("/{groupId:guid}", async (Guid groupId, UpdateGroupRequest request, IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.UpdateAsync(userId, groupId, request, ct);
            return result.ToHttpResult();
        });

        group.MapDelete("/{groupId:guid}/members/{memberId:guid}", async (Guid groupId, Guid memberId, IGroupService groupService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await groupService.RemoveMemberAsync(userId, groupId, memberId, ct);
            return result.ToHttpResult();
        });
    }
}