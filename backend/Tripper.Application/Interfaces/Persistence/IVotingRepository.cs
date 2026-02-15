using Tripper.Application.DTOs;
using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Persistence;

public interface IVotingRepository
{
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<bool> HasActiveSessionAsync(Guid groupId, CancellationToken ct);
    void AddSession(VotingSession session);
    Task<VotingSession?> FindSessionAsync(Guid votingId, CancellationToken ct);
    Task<VotingSession?> FindSessionWithVotesAndCandidatesAsync(Guid votingId, CancellationToken ct);
    Task<VotingSessionResponse?> GetActiveSessionResponseAsync(Guid groupId, Guid currentUserId, CancellationToken ct);
    Task<bool> CandidateExistsAsync(Guid votingId, string city, string country, CancellationToken ct);
    Task<Candidate?> FindCandidateAsync(Guid candidateId, CancellationToken ct);
    void AddCandidate(Candidate candidate);
    Task<int> CountUserVotesAsync(Guid votingId, Guid userId, CancellationToken ct);
    Task<Vote?> FindLatestUserVoteAsync(Guid votingId, Guid userId, Guid candidateId, CancellationToken ct);
    void AddVote(Vote vote);
    void RemoveVote(Vote vote);
    Task<Group?> FindGroupAsync(Guid groupId, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}