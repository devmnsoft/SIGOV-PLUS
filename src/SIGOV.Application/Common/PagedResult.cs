namespace SIGOV.Application.Common;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, long Total);
