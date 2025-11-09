using Safe.Domain.Entities;
using static Safe.Domain.Commands.SafeCommand;

namespace Safe.Application.Models;

public sealed record SafeChangesFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    SafeChangeStatus? Status,
    int Page,
    int PageSize)
{
    public static SafeChangesFilter FromQuery(GetChangesQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize ?? 50, 1, 500);
        var page = Math.Max(query.Page ?? 1, 1);
        return new SafeChangesFilter(query.From, query.To, query.Status, page, pageSize);
    }

    public int Skip => (Page - 1) * PageSize;
}
