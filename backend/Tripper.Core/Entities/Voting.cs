namespace Tripper.Core.Entities;

public class VotingSession
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public VotingStatus Status { get; set; }
    public int MaxVotesPerMember { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

public enum VotingStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2
}

public class Candidate
{
    public Guid Id { get; set; }
    public Guid VotingSessionId { get; set; }
    public VotingSession VotingSession { get; set; } = null!;

    public string CityName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Vote
{
    public Guid Id { get; set; }
    public Guid VotingSessionId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
