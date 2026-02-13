using Tripper.Application.Common;
using Tripper.Application.DTOs;

namespace Tripper.Application.Interfaces;

public interface IGroupService
{
    Task<Result<GroupResponse>> CreateAsync(Guid currentUserId, CreateGroupRequest request, CancellationToken ct);
    Task<Result<List<GroupResponse>>> GetMyGroupsAsync(Guid currentUserId, CancellationToken ct);
    Task<Result<GroupDetailResponse>> GetDetailsAsync(Guid currentUserId, Guid groupId, CancellationToken ct);

    Task<Result> AddMemberAsync(Guid currentUserId, Guid groupId, AddMemberRequest request, CancellationToken ct);
    Task<Result> UpdateAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request, CancellationToken ct);
    Task<Result> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberUserId, CancellationToken ct);
}