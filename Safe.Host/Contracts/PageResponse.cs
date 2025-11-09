using Safe.Application.Models;

namespace Safe.Host.Contracts;

public sealed record PageResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int PageNumber,
    int PageSize)
{
    public static PageResponse<T> From(PageResult<T> result)
        => new(result.Items, result.Total, result.Page, result.PageSize);
}
