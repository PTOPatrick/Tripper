using System.ComponentModel.DataAnnotations;

namespace Tripper.Core.Entities;

public class Group
{
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? DestinationCityName { get; set; }
    public string? DestinationCountry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<Item> Items { get; set; } = new List<Item>();
    public ICollection<VotingSession> VotingSessions { get; set; } = new List<VotingSession>();
}

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public GroupRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum GroupRole
{
    Contributor = 0,
    Admin = 1
}
