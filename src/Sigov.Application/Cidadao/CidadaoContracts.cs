using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Cidadao;

public sealed record CidadaoContexto(long TenantId, long EntidadeId, long? PessoaId, long? UsuarioId, string CorrelationId, string? Ip);
public sealed record CidadaoServico(long Id, string Nome, string Categoria, string Descricao, string PublicoAlvo, int PrazoDias, string Canal, string? UnidadeResponsavel, string? Requisitos, bool Destaque);
public sealed record CidadaoHistorico(string Status, string Descricao, DateTimeOffset RegistradoEm);
public sealed class CidadaoSolicitacao
{
    public long Id { get; set; }
    public string Protocolo { get; set; } = string.Empty;
    public string CodigoVerificador { get; set; } = string.Empty;
    public string Servico { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset? PrazoEm { get; set; }
    public string? UnidadeResponsavel { get; set; }
    public IReadOnlyList<CidadaoHistorico> Historico { get; set; } = [];
}
public sealed record CidadaoDashboard(long Abertas, long Vencidas, long OuvidoriasAbertas, long AgendamentosHoje, decimal? AvaliacaoMedia);

public sealed class AbrirSolicitacaoRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "Selecione um serviço publicado.")]
    [Display(Name = "Serviço")]
    public long ServicoId { get; set; }

    [Required, StringLength(2000, MinimumLength = 10)]
    [Display(Name = "Descreva sua necessidade")]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe um e-mail para comunicações."), EmailAddress, StringLength(254)]
    [Display(Name = "E-mail para avisos")]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Telefone (opcional)")]
    public string? Telefone { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirme a ciência sobre o tratamento de dados.")]
    [Display(Name = "Li a finalidade e autorizo o tratamento necessário para este serviço")]
    public bool AceiteLgpd { get; set; }
}

public interface ICidadaoRepository
{
    Task<IReadOnlyList<CidadaoServico>> ListarServicosAsync(CidadaoContexto contexto, string? busca, string? categoria, CancellationToken ct);
    Task<CidadaoServico?> ObterServicoAsync(CidadaoContexto contexto, long id, CancellationToken ct);
    Task<CidadaoSolicitacao> AbrirSolicitacaoAsync(CidadaoContexto contexto, AbrirSolicitacaoRequest request, CancellationToken ct);
    Task<IReadOnlyList<CidadaoSolicitacao>> MinhasSolicitacoesAsync(CidadaoContexto contexto, CancellationToken ct);
    Task<CidadaoSolicitacao?> ConsultarProtocoloAsync(CidadaoContexto contexto, string protocolo, string verificador, bool proprietario, CancellationToken ct);
    Task<CidadaoDashboard> DashboardAsync(CidadaoContexto contexto, CancellationToken ct);
}
