using Microsoft.EntityFrameworkCore;
using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Application.Services;

public sealed class VotingService(TripperDbContext db) : IVotingService
{
    public async Task<Result<VotingSessionResponse>> StartAsync(Guid groupId, Guid currentUserId, CreateVotingRequest request, CancellationToken ct = default)
    {
        // Auth: Member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (request.MaxVotesPerMember <= 0)
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Validation, "voting.maxvotes.invalid", "MaxVotesPerMember must be > 0."));

        var hasActive = await db.VotingSessions
            .AnyAsync(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open, ct);

        if (hasActive)
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Conflict, "voting.active_exists", "An active voting session already exists."));

        var session = new VotingSession
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Status = VotingStatus.Open,
            MaxVotesPerMember = request.MaxVotesPerMember,
            CreatedAt = DateTime.UtcNow
        };

        db.VotingSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return Result<VotingSessionResponse>.Ok(new VotingSessionResponse(
            session.Id, session.GroupId, session.Status, session.MaxVotesPerMember, session.CreatedAt, null, new List<CandidateDto>()));
    }

    public async Task<Result<VotingSessionResponse?>> GetActiveAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        // Auth: Member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result<VotingSessionResponse?>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var session = await db.VotingSessions
            .AsNoTracking()
            .Include(vs => vs.Candidates)
                .ThenInclude(c => c.CreatedByUser)
            .Include(vs => vs.Votes)
            .FirstOrDefaultAsync(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open, ct);

        if (session is null)
            return Result<VotingSessionResponse?>.Ok(null); // mapped to NoContent in endpoint

        var response = new VotingSessionResponse(
            session.Id, session.GroupId, session.Status, session.MaxVotesPerMember, session.CreatedAt, session.ClosedAt,
            session.Candidates.Select(c => new CandidateDto(
                c.Id, c.CityName, c.Country, c.CreatedByUserId, c.CreatedByUser.Username,
                session.Votes.Count(v => v.CandidateId == c.Id)
            )).ToList()
        );

        return Result<VotingSessionResponse?>.Ok(response);
    }

    public async Task<Result> AddCandidateAsync(Guid groupId, Guid votingId, Guid currentUserId, AddCandidateRequest request, CancellationToken ct = default)
    {
        // Auth: Member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (string.IsNullOrWhiteSpace(request.CityName) || string.IsNullOrWhiteSpace(request.Country))
            return Result.Fail(new Error(ErrorType.Validation, "candidate.invalid_input", "CityName and Country are required."));

        var session = await db.VotingSessions.FirstOrDefaultAsync(vs => vs.Id == votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result.Fail(new Error(ErrorType.Validation, "voting.not_open", "Voting session is not open."));

        // Duplicate check (case-insensitive)
        var city = request.CityName.Trim();
        var country = request.Country.Trim();

        var exists = await db.Candidates.AnyAsync(c =>
            c.VotingSessionId == votingId &&
            c.CityName.ToLower() == city.ToLower() &&
            c.Country.ToLower() == country.ToLower(), ct);

        if (exists)
            return Result.Fail(new Error(ErrorType.Conflict, "candidate.exists", "Candidate already exists."));

        db.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(),
            VotingSessionId = votingId,
            CityName = city,
            Country = country,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CastVoteAsync(Guid groupId, Guid votingId, Guid currentUserId, CastVoteRequest request, CancellationToken ct = default)
    {
        // Auth: Member
        var isMember = await db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (!isMember)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var session = await db.VotingSessions.FirstOrDefaultAsync(vs => vs.Id == votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result.Fail(new Error(ErrorType.Validation, "voting.not_open", "Voting session is not open."));

        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == request.CandidateId, ct);
        if (candidate is null || candidate.VotingSessionId != votingId)
            return Result.Fail(new Error(ErrorType.NotFound, "candidate.not_found", "Candidate not found."));

        // Check max votes
        var userVoteCount = await db.Votes.CountAsync(v => v.VotingSessionId == votingId && v.UserId == currentUserId, ct);
        if (userVoteCount >= session.MaxVotesPerMember)
            return Result.Fail(new Error(ErrorType.Validation, "vote.max_reached", $"You have reached the maximum of {session.MaxVotesPerMember} votes."));

        db.Votes.Add(new Vote
        {
            Id = Guid.NewGuid(),
            VotingSessionId = votingId,
            CandidateId = request.CandidateId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<CloseVotingResponse>> CloseAsync(Guid groupId, Guid votingId, Guid currentUserId, CancellationToken ct = default)
    {
        // Auth: Admin
        var member = await db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);
        if (member is null)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."));

        if (member.Role != GroupRole.Admin)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var session = await db.VotingSessions
            .Include(vs => vs.Votes)
            .Include(vs => vs.Candidates)
            .FirstOrDefaultAsync(vs => vs.Id == votingId, ct);

        if (session is null || session.GroupId != groupId)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.Validation, "voting.not_open", "Session is not open."));

        // Winner calculation
        Candidate? winner = null;

        var voteCounts = session.Votes
            .GroupBy(v => v.CandidateId)
            .Select(g => new { CandidateId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        if (voteCounts.Count != 0)
        {
            var maxVotes = voteCounts.First().Count;
            var topIds = voteCounts.Where(x => x.Count == maxVotes).Select(x => x.CandidateId).ToList();

            winner = topIds.Count == 1
                ? session.Candidates.First(c => c.Id == topIds[0])
                : session.Candidates.Where(c => topIds.Contains(c.Id)).OrderBy(c => c.CreatedAt).First();
        }

        session.Status = VotingStatus.Closed;
        session.ClosedAt = DateTime.UtcNow;

        if (winner is not null)
        {
            var groupEntity = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
            if (groupEntity is not null)
            {
                groupEntity.DestinationCityName = winner.CityName;
                groupEntity.DestinationCountry = winner.Country;
                groupEntity.ModifiedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);

        return Result<CloseVotingResponse>.Ok(new CloseVotingResponse(winner?.CityName, winner?.Country));
    }
}