using Safe.Domain.Entities;

namespace Safe.Domain.Commands;

public class SafeCommand
{
    public sealed record CreateChangeCommand(
        SafeChangeReason Reason,
        SafeChangeDirection? Direction,
        decimal Amount,
        string Category,
        string Comment,
        DateTimeOffset? OccurredAt = null);

    public sealed record ReverseChangeCommand(
        long ChangeId,
        string Comment);

    public sealed record GetChangesQuery(
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        SafeChangeStatus? Status = null,
        int? Page = null,
        int? PageSize = null);

    public sealed record GetBalanceQuery();
}