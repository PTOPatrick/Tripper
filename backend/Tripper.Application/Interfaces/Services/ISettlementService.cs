using Tripper.Application.DTOs;

namespace Tripper.Application.Interfaces.Services;

public interface ISettlementService
{
    Task<SettlementDtos.SettlementSnapshotDto> RecalculateAsync(Guid groupId, Guid currentUserId, CancellationToken ct);
}