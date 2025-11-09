using Safe.Application.Models;
using Safe.Application.Repositories;
using Safe.Domain.DTOs;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Handlers;

public sealed class GetSafeChangesPageHandler(ISafeChangeReadRepository readRepository)
{
    public Task<PageResult<SafeChangeDto>> HandleAsync(GetChangesQuery query, CancellationToken ct)
    {
        var filter = SafeChangesFilter.FromQuery(query);
        return readRepository.GetChangesAsync(filter, ct);
    }
}
