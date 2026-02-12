using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tripper.Core.DTOs;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.API.Endpoints;

public static class VotingEndpoints
{
    public static void MapVotingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups/{groupId:guid}/votings")
            .WithTags("Voting")
            .RequireAuthorization();

        // Start Voting
        group.MapPost("/", async (Guid groupId, CreateVotingRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Member
            var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            // Check if active session exists
            var hasActive = await db.VotingSessions
                .AnyAsync(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open);
            
            if (hasActive) return Results.Conflict("An active voting session already exists.");

            var session = new VotingSession
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                Status = VotingStatus.Open,
                MaxVotesPerMember = request.MaxVotesPerMember,
                CreatedAt = DateTime.UtcNow
            };

            db.VotingSessions.Add(session);
            await db.SaveChangesAsync();

            return Results.Ok(new VotingSessionResponse(session.Id, session.GroupId, session.Status, session.MaxVotesPerMember, session.CreatedAt, null, new List<CandidateDto>()));
        });

        // Get Active Voting
        group.MapGet("/active", async (Guid groupId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Member
            var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            var session = await db.VotingSessions
                .Include(vs => vs.Candidates)
                .ThenInclude(c => c.CreatedByUser)
                .Include(vs => vs.Votes)
                .FirstOrDefaultAsync(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open);
            
            if (session == null) return Results.NoContent(); // Or NotFound? NoContent fits "no active session".

            var response = new VotingSessionResponse(
                session.Id, session.GroupId, session.Status, session.MaxVotesPerMember, session.CreatedAt, session.ClosedAt,
                session.Candidates.Select(c => new CandidateDto(
                    c.Id, c.CityName, c.Country, c.CreatedByUserId, c.CreatedByUser.Username,
                    session.Votes.Count(v => v.CandidateId == c.Id)
                )).ToList()
            );

            return Results.Ok(response);
        });

        // Add Candidate
        group.MapPost("/{votingId:guid}/candidates", async (Guid groupId, Guid votingId, AddCandidateRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Member
            var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            var session = await db.VotingSessions.FindAsync(votingId);
            if (session == null || session.GroupId != groupId) return Results.NotFound();
            if (session.Status != VotingStatus.Open) return Results.BadRequest("Voting session is not open.");

            // Normalized key to prevent duplicates in same session?
            // "NormalizedKey unique per session"
            var key = $"{request.CityName.ToLower()}|{request.Country.ToLower()}";
            
            var existing = await db.Candidates
                .AnyAsync(c => c.VotingSessionId == votingId && c.CityName.Equals(request.CityName, StringComparison.CurrentCultureIgnoreCase) && c.Country.Equals(request.Country, StringComparison.CurrentCultureIgnoreCase));
                // Simplified duplicate check for MVP
            
            if (existing) return Results.Conflict("Candidate already exists.");

            var candidate = new Candidate
            {
                Id = Guid.NewGuid(),
                VotingSessionId = votingId,
                CityName = request.CityName,
                Country = request.Country,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            db.Candidates.Add(candidate);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Vote
        group.MapPost("/{votingId:guid}/votes", async (Guid groupId, Guid votingId, CastVoteRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Member
            var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (!isMember) return Results.Forbid();

            var session = await db.VotingSessions.FindAsync(votingId);
            if (session == null || session.GroupId != groupId) return Results.NotFound();
            if (session.Status != VotingStatus.Open) return Results.BadRequest("Voting session is not open.");

            var candidate = await db.Candidates.FindAsync(request.CandidateId);
            if (candidate == null || candidate.VotingSessionId != votingId) return Results.NotFound("Candidate not found.");

            // Check max votes
            var userVoteCount = await db.Votes.CountAsync(v => v.VotingSessionId == votingId && v.UserId == userId);
            if (userVoteCount >= session.MaxVotesPerMember)
            {
                return Results.BadRequest($"You have reached the maximum of {session.MaxVotesPerMember} votes.");
            }

            // Check if already voted for this candidate (optional? usually yes, one vote per candidate per user?)
            // Assuming "MaxVotesPerMember" means distinct votes.
            // If user can vote multiple times for same candidate, remove this check.
            // MVP: Prevent duplicate votes for same candidate?
            // "Any member can add candidates and vote while Open."
            // Usually, user can distribute votes.
            // Let's allow voting for same candidate multiple times unless specified otherwise.
            // Actually, "Vote(Id, ...)" implies individual vote records.

            var vote = new Vote
            {
                Id = Guid.NewGuid(),
                VotingSessionId = votingId,
                CandidateId = request.CandidateId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            db.Votes.Add(vote);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Close Voting (Admin only)
        group.MapPost("/{votingId:guid}/close", async (Guid groupId, Guid votingId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth: Admin
            var member = await db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (member == null) return Results.NotFound();
            if (member.Role != GroupRole.Admin) return Results.Forbid();

            var session = await db.VotingSessions
                .Include(vs => vs.Votes)
                .Include(vs => vs.Candidates)
                .FirstOrDefaultAsync(vs => vs.Id == votingId);
            
            if (session == null || session.GroupId != groupId) return Results.NotFound();
            if (session.Status != VotingStatus.Open) return Results.BadRequest("Session is not open.");

            // Determine winner
            var voteCounts = session.Votes
                .GroupBy(v => v.CandidateId)
                .Select(g => new { CandidateId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();
            
            Candidate? winner = null;
            if (voteCounts.Count != 0)
            {
                var maxVotes = voteCounts.First().Count;
                var topCandidates = voteCounts.Where(x => x.Count == maxVotes).Select(x => x.CandidateId).ToList();

                if (topCandidates.Count == 1)
                {
                    winner = session.Candidates.First(c => c.Id == topCandidates.First());
                }
                else
                {
                    // Tie-breaker: earliest CreatedAt
                    winner = session.Candidates
                        .Where(c => topCandidates.Contains(c.Id))
                        .OrderBy(c => c.CreatedAt)
                        .First();
                }
            }

            session.Status = VotingStatus.Closed;
            session.ClosedAt = DateTime.UtcNow;

            if (winner != null)
            {
                var groupEntity = await db.Groups.FindAsync(groupId);
                if (groupEntity != null)
                {
                    groupEntity.DestinationCityName = winner.CityName;
                    groupEntity.DestinationCountry = winner.Country;
                    groupEntity.ModifiedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { Winner = winner?.CityName });
        });
    }
}
