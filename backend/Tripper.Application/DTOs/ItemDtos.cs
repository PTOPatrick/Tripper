namespace Tripper.Application.DTOs;

public record CreateItemRequest(string Title, decimal Amount, string Currency, string? Description, Guid? PaidByMemberId, List<Guid>? PayeeUserIds);
public record UpdateItemRequest(string Title, decimal Amount, string Currency, string Description, List<Guid> PayeeUserIds);
public record ItemResponse(Guid Id, Guid GroupId, string Title, decimal Amount, string Currency, string Description, Guid PaidByMemberId, string PaidByUsername, List<Guid> PayeeUserIds, DateTime CreatedAt);
public record BalanceDto(Guid UserId, string Username, decimal NetBalance, string Currency);
