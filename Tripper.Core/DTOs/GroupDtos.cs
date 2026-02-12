using Tripper.Core.Entities;

namespace Tripper.Core.DTOs;

public record CreateGroupRequest(string Name, string Description, string? DestinationCityName, string? DestinationCountry);
public record UpdateGroupRequest(string Name, string Description, string? DestinationCityName, string? DestinationCountry);
public record GroupResponse(Guid Id, string Name, string Description, string? DestinationCityName, string? DestinationCountry, DateTime CreatedAt, int MemberCount);
public record GroupDetailResponse(Guid Id, string Name, string Description, string? DestinationCityName, string? DestinationCountry, DateTime CreatedAt, List<GroupMemberDto> Members);
public record GroupMemberDto(Guid UserId, string Username, string Email, GroupRole Role, DateTime JoinedAt);
public record AddMemberRequest(string EmailOrUsername);
