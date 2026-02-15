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
    public DbSet<Core.Entities.Currency> Currencies { get; set; }

    public DbSet<SettlementSnapshot> SettlementSnapshots { get; set; }
    public DbSet<SettlementTransfer> SettlementTransfers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User - Email/Username unique
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
            .HasIndex(x => new { x.GroupId, x.UserId })
            .IsUnique();

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
            .HasForeignKey(v => v.VotingSessionId);

        modelBuilder.Entity<Vote>()
            .HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(v => v.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        // Currency
        modelBuilder.Entity<Core.Entities.Currency>(entity =>
        {
            entity.ToTable("Currencies");
            entity.HasKey(c => c.Code);

            entity.Property(c => c.Code)
                .HasMaxLength(3)
                .IsRequired();
        });

        // SettlementSnapshot
        modelBuilder.Entity<SettlementSnapshot>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.GroupId).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.CreatedByUserId).IsRequired();

            b.Property(x => x.BaseCurrency)
                .HasMaxLength(3)
                .IsRequired();

            // Explicit FK to Group
            b.HasOne<Group>()
                .WithMany()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Transfers)
                .WithOne(x => x.SettlementSnapshot)
                .HasForeignKey(x => x.SettlementSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.GroupId, x.CreatedAt });
        });

        // SettlementTransfer
        modelBuilder.Entity<SettlementTransfer>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.SettlementSnapshotId).IsRequired();
            b.Property(x => x.FromUserId).IsRequired();
            b.Property(x => x.ToUserId).IsRequired();

            b.Property(x => x.Amount)
                .HasPrecision(18, 2);

            b.HasIndex(x => x.SettlementSnapshotId);
        });
    }
}