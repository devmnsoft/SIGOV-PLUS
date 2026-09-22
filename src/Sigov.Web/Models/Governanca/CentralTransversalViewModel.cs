namespace Sigov.Web.Models.Governanca;

public sealed record CentralTransversalItem(string Modulo, string Titulo, string Classificacao, string Status, string? Rota,
    string? Motivo = null, string? Responsavel = null, DateTimeOffset? Prazo = null, DateTimeOffset? VerificadoEm = null);
public sealed class CentralTransversalViewModel
{
    public string Titulo { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public string ContextoNome { get; init; } = string.Empty;
    public bool ContextoSelecionado { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string? Modulo { get; init; }
    public string? Classificacao { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 25;
    public IReadOnlyCollection<CentralTransversalItem> Itens { get; init; } = Array.Empty<CentralTransversalItem>();
}
