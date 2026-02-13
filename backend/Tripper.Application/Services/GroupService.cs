using Microsoft.EntityFrameworkCore;
using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.Application.Services;

public sealed class GroupService(TripperDbContext db) : IGroupService
{
    public async Task<Result<GroupResponse>> CreateAsync(Guid currentUserId, CreateGroupRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<GroupResponse>.Fail(new Error(ErrorType.Validation, "group.name.required", "Group name is required."));

        var now = DateTimeOffset.UtcNow;

        var newGroup = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            DestinationCityName = request.DestinationCityName?.Trim(),
            DestinationCountry = request.DestinationCountry?.Trim(),
            CreatedAt = now.UtcDateTime,
            ModifiedAt = now.UtcDateTime
        };

        var member = new GroupMember
        {
            GroupId = newGroup.Id,
            UserId = currentUserId,
            Role = GroupRole.Admin,
            JoinedAt = now.UtcDateTime
        };

        newGroup.Members.Add(member);

        db.Groups.Add(newGroup);
        await db.SaveChangesAsync(ct);

        return Result<GroupResponse>.Ok(new GroupResponse(
            newGroup.Id, newGroup.Name, newGroup.Description,
            newGroup.DestinationCityName, newGroup.DestinationCountry,
            newGroup.CreatedAt, 1));
    }

    public async Task<Result<List<GroupResponse>>> GetMyGroupsAsync(Guid currentUserId, CancellationToken ct)
    {
        var groups = await db.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == currentUserId)
            .Include(gm => gm.Group)
            .Select(gm => new GroupResponse(
                gm.Group.Id, gm.Group.Name, gm.Group.Description,
                gm.Group.DestinationCityName, gm.Group.DestinationCountry,
                gm.Group.CreatedAt,
                gm.Group.Members.Count
            ))
            .ToListAsync(ct);

        return Result<List<GroupResponse>>.Ok(groups);
    }

    public async Task<Result<GroupDetailResponse>> GetDetailsAsync(Guid currentUserId, Guid groupId, CancellationToken ct)
    {
        var groupMember = await db.GroupMembers
            .AsNoTracking()
            .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);

        if (groupMember is null)
            return Result<GroupDetailResponse>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."));

        var g = groupMember.Group;

        var response = new GroupDetailResponse(
            g.Id, g.Name, g.Description, g.DestinationCityName, g.DestinationCountry, g.CreatedAt,
            g.Members.Select(m => new GroupMemberDto(
                m.UserId, m.User.Username, m.User.Email, m.Role, m.JoinedAt
            )).ToList()
        );

        return Result<GroupDetailResponse>.Ok(response);
    }

    public async Task<Result> AddMemberAsync(Guid currentUserId, Guid groupId, AddMemberRequest request, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
            return Result.Fail(new Error(ErrorType.Validation, "member.identifier.required", "Email or username is required."));

        var userToAdd = await db.Users
            .FirstOrDefaultAsync(u =>
                u.Email == request.EmailOrUsername || u.Username == request.EmailOrUsername, ct);

        if (userToAdd is null)
            return Result.Fail(new Error(ErrorType.Validation, "member.user_not_found", "User not found."));

        var exists = await db.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userToAdd.Id, ct);

        if (exists)
            return Result.Fail(new Error(ErrorType.Conflict, "member.already_exists", "User is already a member."));

        db.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = userToAdd.Id,
            Role = GroupRole.Contributor,
            JoinedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
            return Result.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."));

        group.Name = request.Name.Trim();
        group.Description = request.Description.Trim();
        group.DestinationCityName = request.DestinationCityName?.Trim();
        group.DestinationCountry = request.DestinationCountry?.Trim();
        group.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var memberToRemove = await db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberUserId, ct);

        if (memberToRemove is null)
            return Result.Fail(new Error(ErrorType.NotFound, "member.not_found", "Member not found."));

        // Prevent removing last admin
        if (memberToRemove.Role == GroupRole.Admin)
        {
            var adminCount = await db.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId && gm.Role == GroupRole.Admin, ct);

            if (adminCount <= 1)
                return Result.Fail(new Error(ErrorType.Validation, "group.last_admin", "Cannot remove the last admin."));
        }

        db.GroupMembers.Remove(memberToRemove);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
    
    private async Task<Result<GroupMember>> GetMemberOrNotFound(Guid currentUserId, Guid groupId, CancellationToken ct)
    {
        var member = await db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId, ct);

        return member is null
            ? Result<GroupMember>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."))
            : Result<GroupMember>.Ok(member);
    }
}