using Tripper.Application.Common;
using Tripper.Application.DTOs;

namespace Tripper.Application.Interfaces;

public interface IItemService
{
    Task<Result<ItemResponse>> CreateAsync(Guid groupId, Guid currentUserId, CreateItemRequest request, CancellationToken ct = default);
    Task<Result<List<ItemResponse>>> GetAllAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default);
    Task<Result<List<BalanceDto>>> GetBalancesAsync(Guid groupId, Guid currentUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid groupId, Guid itemId, Guid currentUserId, CancellationToken ct = default);
}