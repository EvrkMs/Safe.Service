namespace Safe.Domain.DTOs;

public sealed record SafeChangeDto(
    long Id,
    string Direction,    // "Credit"/"Debit"
    string Reason,       // "Regular"/"Surplus"/"Shortage"/"Correction"
    decimal Amount,
    string Category,
    string Comment,
    DateTimeOffset OccurredAt,
    DateTimeOffset CreatedAt,
    string Status,       // "Posted"/"Pending"/"Reversed"
    long? ReversalOfChangeId,
    string? ReversalComment,
    DateTimeOffset? ReversedAt);
