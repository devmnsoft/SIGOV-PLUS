using Sigov.Application.Abstractions;

namespace Sigov.Application.Executive;

public sealed record ExecutivoFiltro(string? Modulo = null, string? Status = null, string? Prioridade = null, long? UnidadeId = null, long? ResponsavelId = null, DateOnly? Inicio = null, DateOnly? Fim = null);
public sealed record ExecutivoResumo(long MetasAtivas, long MetasAtrasadas, long PendenciasVencidas, long AlertasCriticos, long AprovacoesPendentes, long IntegracoesFalhas);
public sealed record ExecutivoItem(long Id, string Tipo, string Titulo, string? Modulo, string Status, string? Prioridade, string? Severidade, decimal? Percentual, DateOnly? Prazo, string? Responsavel, DateTimeOffset AtualizadoEm);
public sealed record ExecutivoDashboard(ExecutivoResumo Resumo, IReadOnlyList<ExecutivoItem> Itens, IReadOnlyList<ExecutivoItem> Indicadores, DateTimeOffset AtualizadoEm);

public interface ICentralExecutivaRepository
{
    Task<ExecutivoDashboard> DashboardAsync(long tenantId, long entidadeId, ExecutivoFiltro filtro, CancellationToken ct);
    Task<IReadOnlyList<ExecutivoItem>> ListarAsync(long tenantId, long entidadeId, string recurso, ExecutivoFiltro filtro, CancellationToken ct);
    Task MarcarAlertaCienteAsync(long tenantId, long entidadeId, long alertaId, long? usuarioId, CancellationToken ct);
    Task DecidirAprovacaoAsync(long tenantId, long entidadeId, long aprovacaoId, bool aprovar, string justificativa, long? usuarioId, CancellationToken ct);
    Task RegistrarExportacaoAsync(long tenantId, long entidadeId, string tipo, long? usuarioId, CancellationToken ct);
}

public interface ICentralExecutivaService
{
    Task<ExecutivoDashboard> DashboardAsync(ExecutivoFiltro filtro, CancellationToken ct);
    Task<IReadOnlyList<ExecutivoItem>> ListarAsync(string recurso, ExecutivoFiltro filtro, CancellationToken ct);
    Task MarcarAlertaCienteAsync(long id, long? usuarioId, CancellationToken ct);
    Task DecidirAprovacaoAsync(long id, bool aprovar, string justificativa, long? usuarioId, CancellationToken ct);
    Task<byte[]> ExportarCsvAsync(string recurso, ExecutivoFiltro filtro, long? usuarioId, CancellationToken ct);
}

public sealed class CentralExecutivaService(ICentralExecutivaRepository repository, ICurrentTenant tenant) : ICentralExecutivaService
{
    private (long Tenant, long Entidade) Contexto() => (tenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório."), tenant.EntidadeId ?? throw new InvalidOperationException("Entidade obrigatória."));
    public Task<ExecutivoDashboard> DashboardAsync(ExecutivoFiltro filtro, CancellationToken ct) { var c=Contexto(); return repository.DashboardAsync(c.Tenant,c.Entidade,filtro,ct); }
    public Task<IReadOnlyList<ExecutivoItem>> ListarAsync(string recurso, ExecutivoFiltro filtro, CancellationToken ct) { var c=Contexto(); return repository.ListarAsync(c.Tenant,c.Entidade,recurso,filtro,ct); }
    public Task MarcarAlertaCienteAsync(long id,long? usuarioId,CancellationToken ct) { var c=Contexto(); return repository.MarcarAlertaCienteAsync(c.Tenant,c.Entidade,id,usuarioId,ct); }
    public Task DecidirAprovacaoAsync(long id,bool aprovar,string justificativa,long? usuarioId,CancellationToken ct) { if(string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Justificativa obrigatória."); var c=Contexto(); return repository.DecidirAprovacaoAsync(c.Tenant,c.Entidade,id,aprovar,justificativa.Trim(),usuarioId,ct); }
    public async Task<byte[]> ExportarCsvAsync(string recurso,ExecutivoFiltro filtro,long? usuarioId,CancellationToken ct)
    {
        var itens=await ListarAsync(recurso,filtro,ct).ConfigureAwait(false); var c=Contexto(); await repository.RegistrarExportacaoAsync(c.Tenant,c.Entidade,recurso,usuarioId,ct).ConfigureAwait(false);
        static string Csv(object? value) { var s=Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??string.Empty; if(s.Length>0 && "=+-@\t\r".Contains(s[0])) s="'"+s; return "\""+s.Replace("\"","\"\"")+"\""; }
        var lines=new List<string>{"Id;Tipo;Titulo;Modulo;Status;Prioridade;Severidade;Percentual;Prazo;Responsavel;AtualizadoEm"};
        lines.AddRange(itens.Select(x=>string.Join(';',Csv(x.Id),Csv(x.Tipo),Csv(x.Titulo),Csv(x.Modulo),Csv(x.Status),Csv(x.Prioridade),Csv(x.Severidade),Csv(x.Percentual),Csv(x.Prazo),Csv(x.Responsavel),Csv(x.AtualizadoEm))));
        return System.Text.Encoding.UTF8.GetBytes("\uFEFF"+string.Join(Environment.NewLine,lines));
    }
}
