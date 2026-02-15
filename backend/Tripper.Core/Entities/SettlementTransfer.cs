namespace Tripper.Core.Entities;

public class SettlementTransfer
{
    public Guid Id { get; set; }
    public Guid SettlementSnapshotId { get; set; }
    public SettlementSnapshot? SettlementSnapshot { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public decimal Amount { get; set; } // in CHF
}