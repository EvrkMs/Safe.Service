using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Safe.Application.Repositories;
using Safe.Domain.DTOs;

namespace Safe.Application.Handlers;

public sealed class GetBalanceQueryHandler(
    ISafeChangeReadRepository readRepository,
    IMemoryCache cache,
    ILogger<GetBalanceQueryHandler> logger)
{
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
    };

    public async Task<BalanceDto> HandleAsync(CancellationToken ct)
    {
        var balance = await cache.GetOrCreateAsync<decimal>(
            SafeBalanceCache.CacheKey,
            async entry =>
            {
                entry.SetOptions(CacheOptions);
                var amount = await readRepository.GetBalanceAsync(ct);
                logger.LogDebug("Refreshed balance cache: {Balance}", amount);
                return amount;
            });

        return new BalanceDto(balance);
    }
}
