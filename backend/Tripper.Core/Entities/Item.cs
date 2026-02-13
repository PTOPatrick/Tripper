using System.ComponentModel.DataAnnotations;

namespace Tripper.Core.Entities;

public class Item
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid PaidByMemberId { get; set; }
    // Navigation to the User who paid (not GroupMember directly to avoid complex keys, but can map to user)
    public User PaidByUser { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CHF";
    [Required]
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Simplified for MVP: List of User IDs who are involved in this expense
    public List<Guid> PayeeUserIds { get; set; } = [];
}
