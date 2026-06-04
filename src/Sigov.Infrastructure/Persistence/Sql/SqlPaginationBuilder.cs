namespace Sigov.Infrastructure.Persistence.Sql;

public static class SqlPaginationBuilder
{
    public static (int Limit, int Offset) Build(int page, int pageSize, int maxPageSize = 100)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : Math.Min(pageSize, maxPageSize);
        return (safePageSize, (safePage - 1) * safePageSize);
    }
}
