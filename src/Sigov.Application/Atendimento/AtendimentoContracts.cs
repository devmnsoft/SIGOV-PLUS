namespace Sigov.Application.Atendimento;

public sealed record AtendimentoContexto(long TenantId, long EntidadeId, long? UsuarioId, string CorrelationId, string? Ip);
public sealed record AtendimentoFiltro(string? Busca, string? Status, DateOnly? Inicio = null, DateOnly? Fim = null, int Pagina = 1, int Tamanho = 20);
public sealed record AtendimentoRegistro(long Id, string Codigo, string Descricao, string Status, DateTimeOffset CriadoEm);
public sealed record AtendimentoPagina(IReadOnlyList<AtendimentoRegistro> Items, int Pagina, int Tamanho, long Total);
public sealed record AtendimentoRegistroRequest(string Codigo, string Descricao, string Status, string? Justificativa, string DadosJson = "{}");
public sealed record AtendimentoDashboard(long DemandasAbertas, long DemandasAtrasadas, long OuvidoriaPendentes, long EsicPendentes, long AgendamentosHoje, long EncaminhamentosPendentes, decimal? SatisfacaoMedia, IReadOnlyList<AtendimentoRegistro>? ServicosMaisSolicitados = null);

public interface IAtendimentoRepository
{
    Task<AtendimentoDashboard> DashboardAsync(AtendimentoContexto contexto, AtendimentoFiltro filtro, CancellationToken ct);
    Task<AtendimentoPagina> ListarAsync(AtendimentoContexto contexto, string recurso, AtendimentoFiltro filtro, bool podeVerSigilo, CancellationToken ct);
    Task<long> SalvarAsync(AtendimentoContexto contexto, string recurso, AtendimentoRegistroRequest request, long? id, CancellationToken ct);
    Task ExcluirAsync(AtendimentoContexto contexto, string recurso, long id, string justificativa, CancellationToken ct);
    Task<byte[]> ExportarCsvAsync(AtendimentoContexto contexto, string relatorio, AtendimentoFiltro filtro, CancellationToken ct);
}
