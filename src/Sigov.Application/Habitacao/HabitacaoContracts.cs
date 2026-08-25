namespace Sigov.Application.Habitacao;

public sealed record HabitacaoContexto(long TenantId,long EntidadeId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record HabitacaoFiltro(string? Busca,string? Status,int Pagina=1,int Tamanho=25);
public sealed record HabitacaoRegistro(long Id,string Codigo,string Descricao,string Status,DateTimeOffset CriadoEm);
public sealed record HabitacaoPagina(IReadOnlyList<HabitacaoRegistro> Itens,int Pagina,int Tamanho,long Total);
public sealed record HabitacaoRegistroRequest(string? Codigo,string Descricao,string Status,string? Justificativa,string DadosJson="{}");
public sealed record HabitacaoDashboard(long Familias,long InscricoesAtivas,long VulnerabilidadeAlta,long ProgramasAtivos,long VisitasPendentes,long Disponiveis,long Regularizacoes,long Beneficiarios,long TermosPendentes);
public interface IHabitacaoRepository
{
 Task<HabitacaoDashboard> DashboardAsync(HabitacaoContexto contexto,CancellationToken ct);
 Task<HabitacaoPagina> ListarAsync(HabitacaoContexto contexto,string recurso,HabitacaoFiltro filtro,CancellationToken ct);
 Task<long> SalvarAsync(HabitacaoContexto contexto,string recurso,HabitacaoRegistroRequest request,long? id,CancellationToken ct);
 Task ExcluirAsync(HabitacaoContexto contexto,string recurso,long id,string justificativa,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(HabitacaoContexto contexto,string recurso,CancellationToken ct);
}
