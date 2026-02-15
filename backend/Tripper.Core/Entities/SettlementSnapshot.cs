namespace Tripper.Core.Entities;

public class SettlementSnapshot
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string BaseCurrency { get; set; } = "CHF";
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime? RatesAsOfUtc { get; set; }
    public int ItemsIncludedCount { get; set; }
    public List<SettlementTransfer> Transfers { get; set; } = new();
}