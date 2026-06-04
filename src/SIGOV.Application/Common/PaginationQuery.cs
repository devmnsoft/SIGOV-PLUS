namespace SIGOV.Application.Common;

public sealed record PaginationQuery(int Page = 1, int PageSize = 20)
{
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 100 ? 20 : PageSize;
    public int Offset => (SafePage - 1) * SafePageSize;
}
