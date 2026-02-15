using Microsoft.EntityFrameworkCore;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Infra.Repositories;

public sealed class VotingRepository(TripperDbContext db) : IVotingRepository
{
    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<bool> HasActiveSessionAsync(Guid groupId, CancellationToken ct) =>
        db.VotingSessions.AnyAsync(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open, ct);

    public void AddSession(VotingSession session) => db.VotingSessions.Add(session);

    public Task<VotingSession?> FindSessionAsync(Guid votingId, CancellationToken ct) =>
        db.VotingSessions.FirstOrDefaultAsync(vs => vs.Id == votingId, ct);

    public Task<VotingSession?> FindSessionWithVotesAndCandidatesAsync(Guid votingId, CancellationToken ct) =>
        db.VotingSessions
            .Include(vs => vs.Votes)
            .Include(vs => vs.Candidates)
            .FirstOrDefaultAsync(vs => vs.Id == votingId, ct);

    // Active session as response DTO (avoid Include-cycle issues and keep it fast)
    public Task<VotingSessionResponse?> GetActiveSessionResponseAsync(Guid groupId, Guid currentUserId, CancellationToken ct) =>
        db.VotingSessions
            .AsNoTracking()
            .Where(vs => vs.GroupId == groupId && vs.Status == VotingStatus.Open)
            .Select(vs => new VotingSessionResponse(
                vs.Id,
                vs.GroupId,
                vs.Status,
                vs.MaxVotesPerMember,
                vs.CreatedAt,
                vs.ClosedAt,
                vs.Candidates
                    .Select(c => new CandidateDto(
                        c.Id,
                        c.CityName,
                        c.Country,
                        c.CreatedByUserId,
                        c.CreatedByUser.Username,
                        vs.Votes.Count(v => v.CandidateId == c.Id),
                        vs.Votes.Count(v => v.CandidateId == c.Id && v.UserId == currentUserId)
                    ))
                    .ToList()
            ))
            .FirstOrDefaultAsync(ct);

    public Task<bool> CandidateExistsAsync(Guid votingId, string city, string country, CancellationToken ct)
        => db.Candidates.AnyAsync(c =>
            c.VotingSessionId == votingId &&
            c.CityName.ToLower() == city.ToLower() &&
            c.Country.ToLower() == country.ToLower(), ct);

    public Task<Candidate?> FindCandidateAsync(Guid candidateId, CancellationToken ct) =>
        db.Candidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct);

    public void AddCandidate(Candidate candidate) => db.Candidates.Add(candidate);

    public Task<int> CountUserVotesAsync(Guid votingId, Guid userId, CancellationToken ct) =>
        db.Votes.CountAsync(v => v.VotingSessionId == votingId && v.UserId == userId, ct);

    public Task<Vote?> FindLatestUserVoteAsync(Guid votingId, Guid userId, Guid candidateId, CancellationToken ct) =>
        db.Votes
            .Where(v => v.VotingSessionId == votingId && v.UserId == userId && v.CandidateId == candidateId)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public void AddVote(Vote vote) => db.Votes.Add(vote);
    public void RemoveVote(Vote vote) => db.Votes.Remove(vote);

    public Task<Group?> FindGroupAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}