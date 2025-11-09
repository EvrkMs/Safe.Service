using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Safe.Application.Factories;
using Safe.Application.Repositories;
using Safe.Domain.Entities;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Handlers;

public sealed class ReverseSafeChangeHandler(
    ISafeChangeWriteRepository writeRepository,
    SafeChangeFactory factory,
    IMemoryCache cache,
    ILogger<ReverseSafeChangeHandler> logger)
{
    public async Task HandleAsync(ReverseChangeCommand cmd, string currentUser, CancellationToken ct)
    {
        var original = await writeRepository.GetTrackedAsync(cmd.ChangeId, ct)
                       ?? throw new KeyNotFoundException($"Запись {cmd.ChangeId} не найдена");

        if (original.Status == SafeChangeStatus.Reversed)
        {
            throw new InvalidOperationException("Уже отменена.");
        }

        var reversal = factory.CreateReversal(original, cmd.Comment, currentUser);

        original.Status = SafeChangeStatus.Reversed;
        original.ReversalComment = cmd.Comment;
        original.ReversedAt = DateTimeOffset.UtcNow;
        original.ModifiedBy = currentUser;
        original.ModifiedAt = DateTimeOffset.UtcNow;

        await writeRepository.CompleteReversalAsync(original, reversal, ct);
        cache.Remove(SafeBalanceCache.CacheKey);

        logger.LogInformation(
            "Reversed SafeChange {OriginalId} → created reversal {ReversalId} by {User}",
            original.Id,
            reversal.Id,
            currentUser);
    }
}
