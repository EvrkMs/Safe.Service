using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Safe.Application.Factories;
using Safe.Application.Repositories;
using static Safe.Domain.Commands.SafeCommand;
using FluentValidationException = FluentValidation.ValidationException;

namespace Safe.Application.Handlers;

public sealed class CreateSafeChangeHandler(
    IValidator<CreateChangeCommand> validator,
    ISafeChangeWriteRepository writeRepository,
    SafeChangeFactory factory,
    IMemoryCache cache,
    ILogger<CreateSafeChangeHandler> logger)
{
    public async Task<long> HandleAsync(CreateChangeCommand cmd, string currentUser, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(cmd, ct);
        if (!validationResult.IsValid)
        {
            throw new FluentValidationException(validationResult.Errors);
        }

        var entity = factory.Create(cmd, currentUser);
        var id = await writeRepository.AddAsync(entity, ct);
        cache.Remove(SafeBalanceCache.CacheKey);

        logger.LogInformation(
            "Created SafeChange {Id} by {User}: {Direction} {Amount} ({Reason})",
            id,
            currentUser,
            entity.Direction,
            entity.Amount,
            cmd.Reason);

        return id;
    }
}
