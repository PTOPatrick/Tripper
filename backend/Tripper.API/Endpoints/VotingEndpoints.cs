using System.Security.Claims;
using Tripper.API.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Services;

namespace Tripper.API.Endpoints;

public static class VotingEndpoints
{
    public static void MapVotingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups/{groupId:guid}/votings")
            .WithTags("Voting")
            .RequireAuthorization();

        group.MapPost("/", async (Guid groupId, CreateVotingRequest request, IVotingService votingService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return (await votingService.StartAsync(groupId, userId, request, ct)).ToHttpResult();
        });

        group.MapGet("/active", async (Guid groupId, IVotingService votingService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await votingService.GetActiveAsync(groupId, userId, ct);

            // Special-case: Ok(null) => NoContent (matches your old behavior)
            return result is { IsSuccess: true, Value: null } ? Results.NoContent() : result.ToHttpResult();
        });
        
        group.MapDelete("/{votingId:guid}/votes/{candidateId:guid}", async (Guid groupId, Guid votingId, Guid candidateId, IVotingService svc, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return (await svc.RemoveVoteAsync(groupId, votingId, userId, candidateId, ct)).ToHttpResult();
        });

        group.MapPost("/{votingId:guid}/candidates", async (Guid groupId, Guid votingId, AddCandidateRequest request, IVotingService votingService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return (await votingService.AddCandidateAsync(groupId, votingId, userId, request, ct)).ToHttpResult();
        });

        group.MapPost("/{votingId:guid}/votes", async (Guid groupId, Guid votingId, CastVoteRequest request, IVotingService votingService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return (await votingService.CastVoteAsync(groupId, votingId, userId, request, ct)).ToHttpResult();
        });

        group.MapPost("/{votingId:guid}/close", async (Guid groupId, Guid votingId, IVotingService votingService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return (await votingService.CloseAsync(groupId, votingId, userId, ct)).ToHttpResult();
        });
    }
}