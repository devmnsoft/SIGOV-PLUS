namespace Sigov.Application.Ambiental;
public sealed record AmbientalContexto(long TenantId,long EntidadeId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record AmbientalFiltro(string? Busca,string? Status,DateOnly? Inicio=null,DateOnly? Fim=null,string? Tipo=null,string? Atividade=null,string? Localidade=null,string? Responsavel=null,long? EmpreendedorId=null,int Pagina=1,int Tamanho=20);
public sealed record AmbientalDashboard(long RequerimentosAbertos,long EmAnalise,long PendenciasDocumentais,long VistoriasAgendadas,long LicencasVigentes,long LicencasVencidas,long LicencasAVencer,long CondicionantesPendentes,long CondicionantesVencidas,long DenunciasAbertas,long AutosEmitidos,long TaxasPendentes);
public sealed record AmbientalRegistro(long Id,string Codigo,string Descricao,string Status,decimal? Valor,DateOnly? Data,string DadosJson,DateTimeOffset CriadoEm);
public sealed record AmbientalPagina(IReadOnlyList<AmbientalRegistro> Items,int Pagina,int Tamanho,long Total);
public sealed record AmbientalRegistroRequest(string Codigo,string Descricao,string Status,decimal? Valor,DateOnly? Data,string? Justificativa,string DadosJson="{}");
public interface IAmbientalRepository
{
 Task<AmbientalDashboard> DashboardAsync(AmbientalContexto contexto,AmbientalFiltro filtro,CancellationToken ct);
 Task<AmbientalPagina> ListarAsync(AmbientalContexto contexto,string recurso,AmbientalFiltro filtro,CancellationToken ct);
 Task<long> SalvarAsync(AmbientalContexto contexto,string recurso,AmbientalRegistroRequest request,long? id,CancellationToken ct);
 Task ExcluirAsync(AmbientalContexto contexto,string recurso,long id,string justificativa,CancellationToken ct);
 Task<byte[]> ExportarCsvAsync(AmbientalContexto contexto,string relatorio,AmbientalFiltro filtro,CancellationToken ct);
}
