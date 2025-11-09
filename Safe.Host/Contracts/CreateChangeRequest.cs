using System.ComponentModel.DataAnnotations;
using Safe.Domain.Entities;

namespace Safe.Host.Contracts;

public sealed record CreateChangeRequest(
    [Required] SafeChangeReason Reason,
    SafeChangeDirection? Direction,
    [Required, Range(0.01, 1_000_000_000)] decimal Amount,
    [Required, StringLength(64, MinimumLength = 1)] string Category,
    [Required, StringLength(512, MinimumLength = 1)] string Comment,
    DateTimeOffset? OccurredAt);
