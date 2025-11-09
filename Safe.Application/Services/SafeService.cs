using Microsoft.AspNetCore.Http;
using Safe.Application.Handlers;
using Safe.Application.Models;
using Safe.Domain.DTOs;
using System.Security.Claims;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Services;

public sealed class SafeService(
    CreateSafeChangeHandler createHandler,
    ReverseSafeChangeHandler reverseHandler,
    GetBalanceQueryHandler balanceHandler,
    GetSafeChangeDetailsHandler detailsHandler,
    GetSafeChangesPageHandler pageHandler,
    IHttpContextAccessor httpContextAccessor) : ISafeService
{
    private string GetCurrentUser()
        => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

    public Task<long> CreateAsync(CreateChangeCommand cmd, CancellationToken ct)
        => createHandler.HandleAsync(cmd, GetCurrentUser(), ct);

    public Task ReverseAsync(ReverseChangeCommand cmd, CancellationToken ct)
        => reverseHandler.HandleAsync(cmd, GetCurrentUser(), ct);

    public Task<BalanceDto> GetBalanceAsync(CancellationToken ct)
        => balanceHandler.HandleAsync(ct);

    public Task<SafeChangeDto?> GetByIdAsync(long id, CancellationToken ct)
        => detailsHandler.HandleAsync(id, ct);

    public Task<PageResult<SafeChangeDto>> GetChangesAsync(GetChangesQuery query, CancellationToken ct)
        => pageHandler.HandleAsync(query, ct);
}
