using Tripper.Application.Common;
using Tripper.Application.DTOs;
using Tripper.Application.Interfaces;
using Tripper.Application.Interfaces.Persistence;
using Tripper.Application.Interfaces.Services;
using Tripper.Core.Entities;

namespace Tripper.Application.Services;

public sealed class GroupService(IGroupRepository groups, IUserLookupRepository users) : IGroupService
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

        groups.AddGroup(newGroup);
        await groups.SaveChangesAsync(ct);

        return Result<GroupResponse>.Ok(new GroupResponse(
            newGroup.Id, newGroup.Name, newGroup.Description,
            newGroup.DestinationCityName, newGroup.DestinationCountry,
            newGroup.CreatedAt, 1));
    }

    public async Task<Result<List<GroupResponse>>> GetMyGroupsAsync(Guid currentUserId, CancellationToken ct)
        => Result<List<GroupResponse>>.Ok(await groups.GetMyGroupsAsync(currentUserId, ct));

    public async Task<Result<GroupDetailResponse>> GetDetailsAsync(Guid currentUserId, Guid groupId, CancellationToken ct)
    {
        var response = await groups.GetGroupDetailsAsync(groupId, currentUserId, ct);

        return response is null
            ? Result<GroupDetailResponse>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."))
            : Result<GroupDetailResponse>.Ok(response);
    }

    public async Task<Result> AddMemberAsync(Guid currentUserId, Guid groupId, AddMemberRequest request, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
            return Result.Fail(new Error(ErrorType.Validation, "member.identifier.required", "Email or username is required."));

        var userToAdd = await users.FindByEmailOrUsernameAsync(request.EmailOrUsername, ct);
        if (userToAdd is null)
            return Result.Fail(new Error(ErrorType.Validation, "member.user_not_found", "User not found."));

        if (await groups.MemberExistsAsync(groupId, userToAdd.Id, ct))
            return Result.Fail(new Error(ErrorType.Conflict, "member.already_exists", "User is already a member."));

        groups.AddGroupMember(new GroupMember
        {
            GroupId = groupId,
            UserId = userToAdd.Id,
            Role = GroupRole.Contributor,
            JoinedAt = DateTime.UtcNow
        });

        await groups.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var group = await groups.FindGroupAsync(groupId, ct);
        if (group is null)
            return Result.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."));

        group.Name = request.Name.Trim();
        group.Description = request.Description.Trim();
        group.DestinationCityName = request.DestinationCityName?.Trim();
        group.DestinationCountry = request.DestinationCountry?.Trim();
        group.ModifiedAt = DateTime.UtcNow;

        await groups.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberUserId, CancellationToken ct)
    {
        var auth = await GetMemberOrNotFound(currentUserId, groupId, ct);
        if (!auth.IsSuccess) return Result.Fail(auth.Error!);

        if (auth.Value!.Role != GroupRole.Admin)
            return Result.Fail(new Error(ErrorType.Forbidden, "group.admin_required", "Admin role required."));

        var memberToRemove = await groups.GetMemberAsync(groupId, memberUserId, ct);
        if (memberToRemove is null)
            return Result.Fail(new Error(ErrorType.NotFound, "member.not_found", "Member not found."));

        if (memberToRemove.Role == GroupRole.Admin)
        {
            var adminCount = await groups.CountAdminsAsync(groupId, ct);
            if (adminCount <= 1)
                return Result.Fail(new Error(ErrorType.Validation, "group.last_admin", "Cannot remove the last admin."));
        }

        groups.RemoveGroupMember(memberToRemove);
        await groups.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private async Task<Result<GroupMember>> GetMemberOrNotFound(Guid currentUserId, Guid groupId, CancellationToken ct)
    {
        var member = await groups.GetMembershipAsync(groupId, currentUserId, ct);

        return member is null
            ? Result<GroupMember>.Fail(new Error(ErrorType.NotFound, "group.not_found", "Group not found."))
            : Result<GroupMember>.Ok(member);
    }
}