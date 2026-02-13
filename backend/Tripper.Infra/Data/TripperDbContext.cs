using Microsoft.EntityFrameworkCore;
using Tripper.Core.Entities;

namespace Tripper.Infra.Data;

public class TripperDbContext(DbContextOptions<TripperDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<VotingSession> VotingSessions { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Vote> Votes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User - Email unique
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // GroupMember composite key
        modelBuilder.Entity<GroupMember>()
            .HasKey(gm => new { gm.GroupId, gm.UserId });

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GroupId);

        modelBuilder.Entity<GroupMember>()
            .HasOne(gm => gm.User)
            .WithMany()
            .HasForeignKey(gm => gm.UserId);

        // Item
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Group)
            .WithMany(g => g.Items)
            .HasForeignKey(i => i.GroupId);
            
        modelBuilder.Entity<Item>()
            .HasOne(i => i.PaidByUser)
            .WithMany()
            .HasForeignKey(i => i.PaidByMemberId)
            .OnDelete(DeleteBehavior.Restrict); 
            // Restrict delete of user if they paid for items? Or Cascade? 
            // Usually keep financial records, so Restrict or SetNull. Let's start with Restrict.

        // Voting
        modelBuilder.Entity<VotingSession>()
            .HasOne(vs => vs.Group)
            .WithMany(g => g.VotingSessions)
            .HasForeignKey(vs => vs.GroupId);

        modelBuilder.Entity<Candidate>()
            .HasOne(c => c.VotingSession)
            .WithMany(vs => vs.Candidates)
            .HasForeignKey(c => c.VotingSessionId);

        modelBuilder.Entity<Vote>()
            .HasOne<VotingSession>()
            .WithMany(vs => vs.Votes)
            .HasForeignKey(v => v.VotingSessionId); // Optimization/denormalization for easier queries
            
        modelBuilder.Entity<Vote>()
            .HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(v => v.CandidateId)
            .OnDelete(DeleteBehavior.Restrict); // Don't delete vote if candidate is deleted? Or Cascade?
            // Actually Cascade is fine for MVP.
            
    }
}
