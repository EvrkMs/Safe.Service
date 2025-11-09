using Safe.Application.Models;
using Safe.Domain.DTOs;

namespace Safe.Application.Repositories;

public interface ISafeChangeReadRepository
{
    Task<SafeChangeDto?> GetByIdAsync(long id, CancellationToken ct);
    Task<PageResult<SafeChangeDto>> GetChangesAsync(SafeChangesFilter filter, CancellationToken ct);
    Task<decimal> GetBalanceAsync(CancellationToken ct);
}
