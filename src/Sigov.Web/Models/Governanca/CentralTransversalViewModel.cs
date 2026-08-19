namespace Sigov.Web.Models.Governanca;

public sealed record CentralTransversalItem(string Modulo, string Titulo, string Classificacao, string Status, string? Rota);
public sealed class CentralTransversalViewModel
{
    public string Titulo { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public IReadOnlyCollection<CentralTransversalItem> Itens { get; init; } = Array.Empty<CentralTransversalItem>();
}
