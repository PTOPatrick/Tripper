using Tripper.Application.Common;
using Tripper.Application.DTOs;

namespace Tripper.Application.Interfaces.Services;

public interface IVotingService
{
    Task<Result<VotingSessionResponse>> StartAsync(Guid groupId, Guid currentUserId, CreateVotingRequest request, CancellationToken ct = default);
    Task<Result<VotingSessionResponse?>> GetActiveAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default);
    Task<Result> AddCandidateAsync(Guid groupId, Guid votingId, Guid currentUserId, AddCandidateRequest request, CancellationToken ct = default);
    Task<Result> CastVoteAsync(Guid groupId, Guid votingId, Guid currentUserId, CastVoteRequest request, CancellationToken ct = default);
    Task<Result<CloseVotingResponse>> CloseAsync(Guid groupId, Guid votingId, Guid currentUserId, CancellationToken ct = default);
    Task<Result> RemoveVoteAsync(Guid groupId, Guid votingId, Guid currentUserId, Guid candidateId, CancellationToken ct = default);
}

public sealed record CloseVotingResponse(string? WinnerCityName, string? WinnerCountry);