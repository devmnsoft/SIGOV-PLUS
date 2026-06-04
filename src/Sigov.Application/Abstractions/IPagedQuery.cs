namespace Sigov.Application.Abstractions;

public interface IPagedQuery
{
    int Page { get; }
    int PageSize { get; }
}
