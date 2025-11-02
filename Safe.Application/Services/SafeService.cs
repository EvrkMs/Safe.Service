using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Safe.Domain.DTOs;
using Safe.Domain.Entities;
using Safe.EntityFramework.Contexts;
using System.Security.Claims;
using static Safe.Domain.Commands.SafeCommand;
using FluentValidationException = FluentValidation.ValidationException; // <-- добавь

namespace Safe.Application.Services;

public sealed class SafeService(
    SafeDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IValidator<CreateChangeCommand> validator,
    ILogger<SafeService> logger) : ISafeService
{
    private string GetCurrentUser() => httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

    public async Task<long> CreateAsync(CreateChangeCommand cmd, CancellationToken ct)
    {
        // Валидация
        var validationResult = await validator.ValidateAsync(cmd, ct);
        if (!validationResult.IsValid)
            throw new FluentValidationException(validationResult.Errors); // <-- используй алиас

        // Вывести Direction из Reason при необходимости
        var direction = cmd.Direction ?? cmd.Reason switch
        {
            SafeChangeReason.Surplus => SafeChangeDirection.Credit,
            SafeChangeReason.Shortage => SafeChangeDirection.Debit,
            _ => throw new ArgumentException("Direction обязателен для Regular/Correction")
        };

        var entity = new SafeChange
        {
            Direction = direction,
            Reason = cmd.Reason,
            Amount = decimal.Round(cmd.Amount, 2),
            Category = cmd.Category,
            Comment = cmd.Comment,
            OccurredAt = cmd.OccurredAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = SafeChangeStatus.Posted,
            CreatedBy = GetCurrentUser()
        };

        db.SafeChanges.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created SafeChange {Id} by {User}: {Direction} {Amount} ({Reason})",
            entity.Id, entity.CreatedBy, direction, entity.Amount, cmd.Reason);

        return entity.Id;
    }

    public async Task ReverseAsync(ReverseChangeCommand cmd, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var original = await db.SafeChanges
                .FirstOrDefaultAsync(x => x.Id == cmd.ChangeId, ct)
                ?? throw new KeyNotFoundException($"Запись {cmd.ChangeId} не найдена");

            if (original.Status == SafeChangeStatus.Reversed)
                throw new InvalidOperationException("Уже отменена.");

            // Создаем реверсирующую запись (обратная операция)
            var reversal = new SafeChange
            {
                Direction = original.Direction == SafeChangeDirection.Credit
                    ? SafeChangeDirection.Debit
                    : SafeChangeDirection.Credit,
                Reason = SafeChangeReason.Correction,
                Amount = original.Amount,
                Category = original.Category,
                Comment = $"Реверс #{original.Id}: {cmd.Comment}",
                OccurredAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = SafeChangeStatus.Posted,
                ReversalOfChangeId = original.Id,
                CreatedBy = GetCurrentUser()
            };

            db.SafeChanges.Add(reversal);

            // Помечаем оригинал как Reversed
            original.Status = SafeChangeStatus.Reversed;
            original.ReversalComment = cmd.Comment;
            original.ReversedAt = DateTimeOffset.UtcNow;
            original.ModifiedBy = GetCurrentUser();
            original.ModifiedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("Reversed SafeChange {OriginalId} → created reversal {ReversalId} by {User}",
                original.Id, reversal.Id, GetCurrentUser());
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BalanceDto> GetBalanceAsync(CancellationToken ct)
    {
        var balance = await db.SafeChanges
            .Where(x => x.Status == SafeChangeStatus.Posted)
            .SumAsync(x => x.Direction == SafeChangeDirection.Credit ? x.Amount : -x.Amount, ct);

        logger.LogDebug("Current balance: {Balance}", balance);
        return new BalanceDto(balance);
    }

    public async Task<SafeChangeDto?> GetByIdAsync(long id, CancellationToken ct)
    {
        return await db.SafeChanges
            .Where(x => x.Id == id)
            .Select(x => new SafeChangeDto(
                x.Id,
                x.Direction.ToString(),
                x.Reason.ToString(),
                x.Amount,
                x.Category,
                x.Comment,
                x.OccurredAt,
                x.CreatedAt,
                x.Status.ToString(),
                x.ReversalOfChangeId,
                x.ReversalComment,
                x.ReversedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<SafeChangeDto> Items, int Total)> GetChangesAsync(GetChangesQuery q, CancellationToken ct)
    {
        var query = db.SafeChanges.AsQueryable();

        if (q.From is not null) query = query.Where(x => x.OccurredAt >= q.From);
        if (q.To is not null) query = query.Where(x => x.OccurredAt < q.To);
        if (q.Status is not null) query = query.Where(x => x.Status == q.Status);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(q.PageSize ?? 50, 1, 500);
        var page = Math.Max(q.Page ?? 1, 1);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SafeChangeDto(
                x.Id,
                x.Direction.ToString(),
                x.Reason.ToString(),
                x.Amount,
                x.Category,
                x.Comment,
                x.OccurredAt,
                x.CreatedAt,
                x.Status.ToString(),
                x.ReversalOfChangeId,
                x.ReversalComment,
                x.ReversedAt))
            .ToListAsync(ct);

        return (items, total);
    }
}