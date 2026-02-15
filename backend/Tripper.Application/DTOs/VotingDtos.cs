using Tripper.Core.Entities;

namespace Tripper.Application.DTOs;

public record CreateVotingRequest(int MaxVotesPerMember = 3);
public record AddCandidateRequest(string CityName, string Country);
public record CastVoteRequest(Guid CandidateId);
public record VotingSessionResponse(Guid Id, Guid GroupId, VotingStatus Status, int MaxVotesPerMember, DateTime CreatedAt, DateTime? ClosedAt, List<CandidateDto> Candidates);
public record CandidateDto(Guid Id, string CityName, string Country, Guid CreatedByUserId, string CreatedByUsername, int VoteCount, int MyVoteCount);
