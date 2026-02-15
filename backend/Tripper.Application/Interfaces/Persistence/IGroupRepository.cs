using Tripper.Application.DTOs;
using Tripper.Core.Entities;

namespace Tripper.Application.Interfaces.Persistence;

public interface IGroupRepository
{
    void AddGroup(Group group);
    void AddGroupMember(GroupMember member);
    void RemoveGroupMember(GroupMember member);
    Task<Group?> FindGroupAsync(Guid groupId, CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<int> CountAdminsAsync(Guid groupId, CancellationToken ct);
    Task<List<GroupResponse>> GetMyGroupsAsync(Guid userId, CancellationToken ct);
    Task<GroupDetailResponse?> GetGroupDetailsAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<bool> MemberExistsAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<GroupMember?> GetMemberAsync(Guid groupId, Guid memberUserId, CancellationToken ct);
}