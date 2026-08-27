using Sigov.Application.Cidadao;

namespace Sigov.Web.Models.Cidadao;

public sealed record CidadaoPortalViewModel(IReadOnlyList<CidadaoServico> Destaques, string? Busca = null, string? Categoria = null);
public sealed record CidadaoSolicitarViewModel(AbrirSolicitacaoRequest Form, IReadOnlyList<CidadaoServico> Servicos, CidadaoServico? Selecionado = null);
public sealed class ConsultaProtocoloViewModel
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(30)]
    [System.ComponentModel.DataAnnotations.Display(Name="Número do protocolo")]
    public string Protocolo { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(12,MinimumLength=12)]
    [System.ComponentModel.DataAnnotations.Display(Name="Código verificador")]
    public string Verificador { get; set; } = string.Empty;
    public CidadaoSolicitacao? Resultado { get; set; }
}
