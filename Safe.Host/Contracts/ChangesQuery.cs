using System.ComponentModel.DataAnnotations;
using Safe.Domain.Entities;

namespace Safe.Host.Contracts;

public sealed record ChangesQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    SafeChangeStatus? Status,
    [Range(1, int.MaxValue)] int? Page,
    [Range(1, 500)] int? PageSize);
