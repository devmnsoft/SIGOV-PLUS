namespace Sigov.Application.Sst;

public sealed record SstDashboard(long ServidoresMonitorados,long AsosCriticos,long ExamesCriticos,long ExposicoesAtivas,long EpisCriticos,long CatsAbertas,long Acidentes,long AfastamentosOcupacionais,long TreinamentosVencidos,long EsocialPendente,long EsocialRejeitado);
public sealed record SstOption(long Id,string Label);
public sealed record SstAso(long Id,long ServidorId,string Servidor,string Tipo,DateOnly DataAso,string Medico,string Resultado,string? Restricao,DateOnly? Validade);
public sealed record SstAsoInput(long ServidorId,string Tipo,DateOnly DataAso,string Medico,string Resultado,string? Restricao,DateOnly? Validade);
public interface ISstRepository
{
 Task<SstDashboard> DashboardAsync(long tenantId,long entidadeId,CancellationToken ct);
 Task<IReadOnlyList<SstAso>> ListarAsosAsync(long tenantId,long entidadeId,CancellationToken ct);
 Task<SstAso?> ObterAsoAsync(long tenantId,long entidadeId,long id,CancellationToken ct);
 Task<IReadOnlyList<SstOption>> ServidoresAsync(long tenantId,long entidadeId,CancellationToken ct);
 Task<long> SalvarAsoAsync(long tenantId,long entidadeId,long? exercicioId,long? id,SstAsoInput input,long? usuarioId,CancellationToken ct);
}
