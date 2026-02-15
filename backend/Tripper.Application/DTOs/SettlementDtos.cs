namespace Tripper.Application.DTOs;

public class SettlementDtos
{
    // TODO: umbenennen in SettlementTransferResponse oder so, damit klar ist, dass das DTOs sind
    public record SettlementTransferDto(Guid FromUserId, string FromUsername, Guid ToUserId, string ToUsername, decimal Amount, string Currency);
    public record SettlementSnapshotDto(Guid Id, Guid GroupId, string BaseCurrency, DateTime CreatedAt, Guid CreatedByUserId, IReadOnlyList<SettlementTransferDto> Transfers);
    public record BalanceDto(Guid UserId, string Username, decimal NetBalance, string Currency);
}