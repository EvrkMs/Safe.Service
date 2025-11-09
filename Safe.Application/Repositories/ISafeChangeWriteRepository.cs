using Safe.Domain.Entities;

namespace Safe.Application.Repositories;

public interface ISafeChangeWriteRepository
{
    Task<long> AddAsync(SafeChange change, CancellationToken ct);
    Task<SafeChange?> GetTrackedAsync(long id, CancellationToken ct);
    Task CompleteReversalAsync(SafeChange original, SafeChange reversal, CancellationToken ct);
}
