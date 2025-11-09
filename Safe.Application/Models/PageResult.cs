namespace Safe.Application.Models;

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
