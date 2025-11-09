using Safe.Domain.Entities;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Factories;

public sealed class SafeChangeFactory
{
    public SafeChange Create(CreateChangeCommand cmd, string createdBy)
    {
        var direction = DetermineDirection(cmd);

        return new SafeChange
        {
            Direction = direction,
            Reason = cmd.Reason,
            Amount = decimal.Round(cmd.Amount, 2, MidpointRounding.AwayFromZero),
            Category = cmd.Category,
            Comment = cmd.Comment,
            OccurredAt = cmd.OccurredAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = SafeChangeStatus.Posted,
            CreatedBy = createdBy
        };
    }

    public SafeChange CreateReversal(SafeChange original, string comment, string createdBy)
    {
        return new SafeChange
        {
            Direction = original.Direction == SafeChangeDirection.Credit
                ? SafeChangeDirection.Debit
                : SafeChangeDirection.Credit,
            Reason = SafeChangeReason.Correction,
            Amount = original.Amount,
            Category = original.Category,
            Comment = $"Реверс #{original.Id}: {comment}",
            OccurredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = SafeChangeStatus.Posted,
            ReversalOfChangeId = original.Id,
            CreatedBy = createdBy
        };
    }

    private static SafeChangeDirection DetermineDirection(CreateChangeCommand cmd)
    {
        if (cmd.Direction is { } direction)
        {
            return direction;
        }

        return cmd.Reason switch
        {
            SafeChangeReason.Surplus => SafeChangeDirection.Credit,
            SafeChangeReason.Shortage => SafeChangeDirection.Debit,
            _ => throw new ArgumentException("Direction обязателен для Regular/Correction")
        };
    }
}
