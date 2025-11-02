using System.Runtime.Serialization;

namespace Safe.Domain.Entities;

public enum SafeChangeDirection
{
    [EnumMember(Value = "Credit")] Credit = 1,
    [EnumMember(Value = "Debit")] Debit = 2
}

public enum SafeChangeReason
{
    [EnumMember(Value = "Regular")] Regular = 1,
    [EnumMember(Value = "Surplus")] Surplus = 2,
    [EnumMember(Value = "Shortage")] Shortage = 3,
    [EnumMember(Value = "Correction")] Correction = 4
}

public enum SafeChangeStatus
{
    [EnumMember(Value = "Posted")] Posted = 1,
    [EnumMember(Value = "Pending")] Pending = 2,
    [EnumMember(Value = "Reversed")] Reversed = 3
}

public class SafeChange
{
    public long Id { get; init; }

    public SafeChangeDirection Direction { get; set; }
    public SafeChangeReason Reason { get; set; } = SafeChangeReason.Regular;

    public decimal Amount { get; set; }
    public string Category { get; set; } = default!;
    public string Comment { get; set; } = default!;

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public SafeChangeStatus Status { get; set; } = SafeChangeStatus.Posted;

    // Реверс: ссылка на оригинальную запись (если это реверс)
    public long? ReversalOfChangeId { get; set; }
    public string? ReversalComment { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }

    // Audit trail
    public string CreatedBy { get; set; } = default!;
    public string? ModifiedBy { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }

    // Optimistic concurrency
    public uint RowVersion { get; set; }
}