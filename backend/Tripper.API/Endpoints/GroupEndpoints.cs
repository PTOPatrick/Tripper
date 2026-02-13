using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tripper.Core.DTOs;
using Tripper.Core.Entities;
using Tripper.Infra.Data;

namespace Tripper.API.Endpoints;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/groups").WithTags("Groups").RequireAuthorization();

        // Create Group
        group.MapPost("/", async (CreateGroupRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var newGroup = new Group
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                DestinationCityName = request.DestinationCityName,
                DestinationCountry = request.DestinationCountry,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

            var member = new GroupMember
            {
                GroupId = newGroup.Id,
                UserId = userId,
                Role = GroupRole.Admin,
                JoinedAt = DateTime.UtcNow
            };

            newGroup.Members.Add(member);
            db.Groups.Add(newGroup);
            await db.SaveChangesAsync();

            return Results.Ok(new GroupResponse(
                newGroup.Id, newGroup.Name, newGroup.Description, 
                newGroup.DestinationCityName, newGroup.DestinationCountry, 
                newGroup.CreatedAt, 1));
        });

        // List Groups
        group.MapGet("/", async (TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var groups = await db.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                .Select(gm => new GroupResponse(
                    gm.Group.Id, gm.Group.Name, gm.Group.Description,
                    gm.Group.DestinationCityName, gm.Group.DestinationCountry,
                    gm.Group.CreatedAt,
                    gm.Group.Members.Count
                ))
                .ToListAsync();

            return Results.Ok(groups);
        });

        // Get Group Detail
        group.MapGet("/{groupId:guid}", async (Guid groupId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var groupMember = await db.GroupMembers
                .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (groupMember == null) return Results.NotFound();

            var g = groupMember.Group;
            var response = new GroupDetailResponse(
                g.Id, g.Name, g.Description, g.DestinationCityName, g.DestinationCountry, g.CreatedAt,
                g.Members.Select(m => new GroupMemberDto(
                    m.UserId, m.User.Username, m.User.Email, m.Role, m.JoinedAt
                )).ToList()
            );

            return Results.Ok(response);
        });

        // Add Member
        group.MapPost("/{groupId:guid}/members", async (Guid groupId, AddMemberRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var currentUserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth check: Must be Admin
            var currentMember = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);
            
            if (currentMember == null) return Results.NotFound();
            if (currentMember.Role != GroupRole.Admin) return Results.Forbid();

            // Find user to add
            var userToAdd = await db.Users
                .FirstOrDefaultAsync(u => u.Email == request.EmailOrUsername || u.Username == request.EmailOrUsername);
            
            if (userToAdd == null) return Results.BadRequest("User not found");

            // Check if already member
            var existingMember = await db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userToAdd.Id);
            
            if (existingMember) return Results.Conflict("User is already a member");

            var newMember = new GroupMember
            {
                GroupId = groupId,
                UserId = userToAdd.Id,
                Role = GroupRole.Contributor,
                JoinedAt = DateTime.UtcNow
            };

            db.GroupMembers.Add(newMember);
            await db.SaveChangesAsync();

            return Results.Ok();
        });

        // Update Group (Admin only)
        group.MapPatch("/{groupId:guid}", async (Guid groupId, UpdateGroupRequest request, TripperDbContext db, ClaimsPrincipal user) =>
        {
             var currentUserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var currentMember = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);
            
            if (currentMember == null) return Results.NotFound();
            if (currentMember.Role != GroupRole.Admin) return Results.Forbid();

            var group = await db.Groups.FindAsync(groupId);
            if (group == null) return Results.NotFound();

            group.Name = request.Name;
            group.Description = request.Description;
            group.DestinationCityName = request.DestinationCityName;
            group.DestinationCountry = request.DestinationCountry;
            group.ModifiedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Remove Member
        group.MapDelete("/{groupId:guid}/members/{memberId:guid}", async (Guid groupId, Guid memberId, TripperDbContext db, ClaimsPrincipal user) =>
        {
            var currentUserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Auth check: Must be Admin
            var currentMember = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == currentUserId);
            
            if (currentMember == null) return Results.NotFound();
            if (currentMember.Role != GroupRole.Admin) return Results.Forbid();

            // Prevent removing self if last admin?
            // "Enforce: group must always have >= 1 Admin."
            
            var memberToRemove = await db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId);
            
            if (memberToRemove == null) return Results.NotFound();

            if (memberToRemove.Role == GroupRole.Admin)
            {
                 // Check if there are other admins
                 var adminCount = await db.GroupMembers.CountAsync(gm => gm.GroupId == groupId && gm.Role == GroupRole.Admin);
                 if (adminCount <= 1)
                 {
                     return Results.BadRequest("Cannot remove the last admin.");
                 }
            }

            db.GroupMembers.Remove(memberToRemove);
            await db.SaveChangesAsync();

            return Results.Ok();
        });
    }
}
