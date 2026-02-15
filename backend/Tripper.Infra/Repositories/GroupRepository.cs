using Microsoft.EntityFrameworkCore;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Infra.Repositories;

public sealed class GroupRepository(TripperDbContext db) : IGroupRepository
{
    public void AddGroup(Group group) => db.Groups.Add(group);
    public void AddGroupMember(GroupMember member) => db.GroupMembers.Add(member);
    public void RemoveGroupMember(GroupMember member) => db.GroupMembers.Remove(member);

    public Task<Group?> FindGroupAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public Task<GroupMember?> GetMembershipAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<int> CountAdminsAsync(Guid groupId, CancellationToken ct) =>
        db.GroupMembers.CountAsync(gm => gm.GroupId == groupId && gm.Role == GroupRole.Admin, ct);

    public Task<bool> MemberExistsAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, ct);

    public Task<GroupMember?> GetMemberAsync(Guid groupId, Guid memberUserId, CancellationToken ct) =>
        db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberUserId, ct);
    
    public Task<List<GroupResponse>> GetMyGroupsAsync(Guid userId, CancellationToken ct) =>
        db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == userId)
            .Select(gm => new GroupResponse(
                gm.Group.Id,
                gm.Group.Name,
                gm.Group.Description,
                gm.Group.DestinationCityName,
                gm.Group.DestinationCountry,
                gm.Group.CreatedAt,
                gm.Group.Members.Count
            ))
            .ToListAsync(ct);

    public Task<GroupDetailResponse?> GetGroupDetailsAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId))
            .Select(g => new GroupDetailResponse(
                g.Id,
                g.Name,
                g.Description,
                g.DestinationCityName,
                g.DestinationCountry,
                g.CreatedAt,
                g.Members.Select(m => new GroupMemberDto(
                    m.UserId,
                    m.User.Username,
                    m.User.Email,
                    m.Role,
                    m.JoinedAt
                )).ToList()
            ))
            .FirstOrDefaultAsync(ct);
}