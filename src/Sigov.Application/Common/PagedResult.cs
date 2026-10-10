using System.Text.Json.Serialization;

namespace Sigov.Application.Common;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, long TotalItems, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Diagnostic = null)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new(Array.Empty<T>(), page, pageSize, 0);

    /// <summary>Paginação vazia com diagnóstico operacional explícito (ex.: schema ausente) em vez de uma lista vazia que simula ausência de dados.</summary>
    public static PagedResult<T> Indisponivel(int page, int pageSize, string diagnostic) => new(Array.Empty<T>(), page, pageSize, 0, diagnostic);
}
