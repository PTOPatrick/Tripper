using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Application.Interfaces.Services;
using Tripper.Core.Entities;

namespace Tripper.Application.Services;

public sealed class VotingService(IVotingRepository repo) : IVotingService
{
    public async Task<Result<VotingSessionResponse>> StartAsync(Guid groupId, Guid currentUserId, CreateVotingRequest request, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (request.MaxVotesPerMember <= 0)
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Validation, "voting.maxvotes.invalid", "MaxVotesPerMember must be > 0."));

        if (await repo.HasActiveSessionAsync(groupId, ct))
            return Result<VotingSessionResponse>.Fail(new Error(ErrorType.Conflict, "voting.active_exists", "An active voting session already exists."));

        var session = new VotingSession
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Status = VotingStatus.Open,
            MaxVotesPerMember = request.MaxVotesPerMember,
            CreatedAt = DateTime.UtcNow
        };

        repo.AddSession(session);
        await repo.SaveChangesAsync(ct);

        return Result<VotingSessionResponse>.Ok(new VotingSessionResponse(
            session.Id, session.GroupId, session.Status, session.MaxVotesPerMember, session.CreatedAt, null, new List<CandidateDto>()));
    }

    public async Task<Result<VotingSessionResponse?>> GetActiveAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result<VotingSessionResponse?>.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var response = await repo.GetActiveSessionResponseAsync(groupId, currentUserId, ct);
        return Result<VotingSessionResponse?>.Ok(response); // null => NoContent im Endpoint
    }

    public async Task<Result> RemoveVoteAsync(Guid groupId, Guid votingId, Guid currentUserId, Guid candidateId, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var session = await repo.FindSessionAsync(votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result.Fail(new Error(ErrorType.Validation, "voting.not_open", "Voting session is not open."));

        var candidate = await repo.FindCandidateAsync(candidateId, ct);
        if (candidate is null || candidate.VotingSessionId != votingId)
            return Result.Fail(new Error(ErrorType.NotFound, "candidate.not_found", "Candidate not found."));

        var vote = await repo.FindLatestUserVoteAsync(votingId, currentUserId, candidateId, ct);
        if (vote is null)
            return Result.Fail(new Error(ErrorType.NotFound, "vote.not_found", "You have no vote to remove for this candidate."));

        repo.RemoveVote(vote);
        await repo.SaveChangesAsync(ct);

        return Result.Ok();
    }

    public async Task<Result> AddCandidateAsync(Guid groupId, Guid votingId, Guid currentUserId, AddCandidateRequest request, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        if (string.IsNullOrWhiteSpace(request.CityName) || string.IsNullOrWhiteSpace(request.Country))
            return Result.Fail(new Error(ErrorType.Validation, "candidate.invalid_input", "CityName and Country are required."));

        var session = await repo.FindSessionAsync(votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result.Fail(new Error(ErrorType.Validation, "voting.not_open", "Voting session is not open."));

        var city = request.CityName.Trim();
        var country = request.Country.Trim();

        if (await repo.CandidateExistsAsync(votingId, city, country, ct))
            return Result.Fail(new Error(ErrorType.Conflict, "candidate.exists", "Candidate already exists."));

        repo.AddCandidate(new Candidate
        {
            Id = Guid.NewGuid(),
            VotingSessionId = votingId,
            CityName = city,
            Country = country,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        await repo.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CastVoteAsync(Guid groupId, Guid votingId, Guid currentUserId, CastVoteRequest request, CancellationToken ct = default)
    {
        if (!await repo.IsMemberAsync(groupId, currentUserId, ct))
            return Result.Fail(new Error(ErrorType.Forbidden, "group.not_member", "You are not a member of this group."));

        var session = await repo.FindSessionAsync(votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result.Fail(new Error(ErrorType.Validation, "voting.not_open", "Voting session is not open."));

        var candidate = await repo.FindCandidateAsync(request.CandidateId, ct);
        if (candidate is null || candidate.VotingSessionId != votingId)
            return Result.Fail(new Error(ErrorType.NotFound, "candidate.not_found", "Candidate not found."));

        var userVoteCount = await repo.CountUserVotesAsync(votingId, currentUserId, ct);
        if (userVoteCount >= session.MaxVotesPerMember)
            return Result.Fail(new Error(ErrorType.Validation, "vote.max_reached", $"You have reached the maximum of {session.MaxVotesPerMember} votes."));

        repo.AddVote(new Vote
        {
            Id = Guid.NewGuid(),
            VotingSessionId = votingId,
            CandidateId = request.CandidateId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        });

        await repo.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<CloseVotingResponse>> CloseAsync(Guid groupId, Guid votingId, Guid currentUserId, CancellationToken ct = default)
    {
        var member = await repo.GetMembershipAsync(groupId, currentUserId, ct);
        if (member is null)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."));

        if (member.Role != GroupRole.Admin)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var session = await repo.FindSessionWithVotesAndCandidatesAsync(votingId, ct);
        if (session is null || session.GroupId != groupId)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.NotFound, "voting.not_found", "Voting session not found."));

        if (session.Status != VotingStatus.Open)
            return Result<CloseVotingResponse>.Fail(new Error(ErrorType.Validation, "voting.not_open", "Session is not open."));

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
            var groupEntity = await repo.FindGroupAsync(groupId, ct);
            if (groupEntity is not null)
            {
                groupEntity.DestinationCityName = winner.CityName;
                groupEntity.DestinationCountry = winner.Country;
                groupEntity.ModifiedAt = DateTime.UtcNow;
            }
        }

        await repo.SaveChangesAsync(ct);

        return Result<CloseVotingResponse>.Ok(new CloseVotingResponse(winner?.CityName, winner?.Country));
    }
}