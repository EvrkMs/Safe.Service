using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Safe.Application.Models;
using Safe.Application.Repositories;
using Safe.Domain.DTOs;
using Safe.Domain.Entities;
using Safe.EntityFramework.Contexts;

namespace Safe.EntityFramework.Repositories;

public sealed class SafeChangeRepository(SafeDbContext db)
    : ISafeChangeReadRepository, ISafeChangeWriteRepository
{
    private static readonly Expression<Func<SafeChange, SafeChangeDto>> Projection = change => new SafeChangeDto(
        change.Id,
        change.Direction.ToString(),
        change.Reason.ToString(),
        change.Amount,
        change.Category,
        change.Comment,
        change.OccurredAt,
        change.CreatedAt,
        change.Status.ToString(),
        change.ReversalOfChangeId,
        change.ReversalComment,
        change.ReversedAt);

    public async Task<long> AddAsync(SafeChange change, CancellationToken ct)
    {
        await db.SafeChanges.AddAsync(change, ct);
        await db.SaveChangesAsync(ct);
        return change.Id;
    }

    public Task<SafeChange?> GetTrackedAsync(long id, CancellationToken ct)
        => db.SafeChanges.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CompleteReversalAsync(SafeChange original, SafeChange reversal, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.SafeChanges.Add(reversal);
        db.SafeChanges.Update(original);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public Task<SafeChangeDto?> GetByIdAsync(long id, CancellationToken ct)
        => db.SafeChanges
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);

    public async Task<PageResult<SafeChangeDto>> GetChangesAsync(SafeChangesFilter filter, CancellationToken ct)
    {
        var query = db.SafeChanges.AsNoTracking();

        if (filter.From is not null)
        {
            query = query.Where(x => x.OccurredAt >= filter.From);
        }

        if (filter.To is not null)
        {
            query = query.Where(x => x.OccurredAt < filter.To);
        }

        if (filter.Status is not null)
        {
            query = query.Where(x => x.Status == filter.Status);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return new PageResult<SafeChangeDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<decimal> GetBalanceAsync(CancellationToken ct)
    {
        return await db.SafeChanges
            .AsNoTracking()
            .Where(x => x.Status != SafeChangeStatus.Pending)
            .SumAsync(x => x.Direction == SafeChangeDirection.Credit ? x.Amount : -x.Amount, ct);
    }
}
