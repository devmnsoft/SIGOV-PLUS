using Sigov.Application.Governanca;

namespace Sigov.Web.Models.Governanca;

public sealed record CentralTransversalItem(long Id, string Modulo, string Titulo, string Classificacao, string Status, string? Rota,
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

public sealed class GovernancaOcorrenciaViewModel
{
    public required GovernancaOcorrenciaDto Ocorrencia { get; init; }
    public string? Retorno { get; init; }
    public IReadOnlyCollection<ResponsavelElegivelDto> Responsaveis { get; init; } = Array.Empty<ResponsavelElegivelDto>();
    public bool PodeAtribuir { get; init; }
    public string? BuscaResponsavel { get; init; }
    public int PaginaResponsavel { get; init; } = 1;
    public bool TemProximaPaginaResponsavel { get; init; }
    public long? ResponsavelInformado { get; init; }
    public string? JustificativaInformada { get; init; }
}
