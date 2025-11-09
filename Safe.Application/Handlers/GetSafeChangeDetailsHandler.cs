using Safe.Application.Repositories;
using Safe.Domain.DTOs;

namespace Safe.Application.Handlers;

public sealed class GetSafeChangeDetailsHandler(ISafeChangeReadRepository readRepository)
{
    public Task<SafeChangeDto?> HandleAsync(long id, CancellationToken ct)
        => readRepository.GetByIdAsync(id, ct);
}
