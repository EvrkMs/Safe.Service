using Safe.Application.Models;
using Safe.Domain.DTOs;

using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Services;

public interface ISafeService
{
    Task<long> CreateAsync(CreateChangeCommand cmd, CancellationToken ct);
    Task ReverseAsync(ReverseChangeCommand cmd, CancellationToken ct);
    Task<BalanceDto> GetBalanceAsync(CancellationToken ct);
    Task<SafeChangeDto?> GetByIdAsync(long id, CancellationToken ct);
    Task<PageResult<SafeChangeDto>> GetChangesAsync(GetChangesQuery query, CancellationToken ct);
}
