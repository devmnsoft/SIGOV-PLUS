namespace Sigov.Application.Juridico;
public sealed record JuridicoContexto(long TenantId,long EntidadeId,long? UsuarioId,string CorrelationId,string? Ip);
public sealed record JuridicoFiltro(string? Busca,string? Status,int Pagina=1,int Tamanho=25);
public sealed record JuridicoRegistro(long Id,string Codigo,string Descricao,string Status,DateTimeOffset CriadoEm);
public sealed record JuridicoPagina(IReadOnlyList<JuridicoRegistro> Itens,int Pagina,int Tamanho,long Total);
public sealed record JuridicoRegistroRequest(string? Codigo,string Descricao,string Status,string? Justificativa,string DadosJson="{}");
public sealed record JuridicoDashboard(long ProcessosAtivos,long PrazosVencidos,long PrazosSeteDias,long IntimacoesPendentes,long AudienciasAgendadas,long PareceresPendentes,long AcordosAtivos,long ObrigacoesVencidas,long DividasEmAberto,decimal CustasPeriodo);
public interface IJuridicoRepository { Task<JuridicoDashboard> DashboardAsync(JuridicoContexto c,CancellationToken ct); Task<JuridicoPagina> ListarAsync(JuridicoContexto c,string recurso,JuridicoFiltro f,CancellationToken ct); Task<long> SalvarAsync(JuridicoContexto c,string recurso,JuridicoRegistroRequest request,long? id,CancellationToken ct); Task ExcluirAsync(JuridicoContexto c,string recurso,long id,string justificativa,CancellationToken ct); Task<byte[]> ExportarCsvAsync(JuridicoContexto c,string recurso,CancellationToken ct); }
