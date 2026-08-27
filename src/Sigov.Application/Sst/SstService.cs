using Sigov.Application.Abstractions;

namespace Sigov.Application.Sst;

public sealed class SstService(ISstRepository repository, ICurrentTenant tenant, ICurrentUser user)
{
 private (long Tenant,long Entidade) Scope() => (tenant.TenantId,tenant.EntidadeId) switch { ({ } t,{ } e) when t>0 && e>0 => (t,e), _ => throw new InvalidOperationException("Tenant e entidade são obrigatórios para acessar o SST360.") };
 public Task<SstDashboard> DashboardAsync(CancellationToken ct){var s=Scope();return repository.DashboardAsync(s.Tenant,s.Entidade,ct);}
 public Task<IReadOnlyList<SstAso>> ListarAsosAsync(CancellationToken ct){var s=Scope();return repository.ListarAsosAsync(s.Tenant,s.Entidade,ct);}
 public Task<SstAso?> ObterAsoAsync(long id,CancellationToken ct){var s=Scope();return repository.ObterAsoAsync(s.Tenant,s.Entidade,id,ct);}
 public Task<IReadOnlyList<SstOption>> ServidoresAsync(CancellationToken ct){var s=Scope();return repository.ServidoresAsync(s.Tenant,s.Entidade,ct);}
 public Task<long> SalvarAsoAsync(long? id,SstAsoInput input,CancellationToken ct)
 {
  if(input.ServidorId<=0||string.IsNullOrWhiteSpace(input.Medico)) throw new ArgumentException("Servidor e médico são obrigatórios.");
  if(!new[]{"admissional","periodico","retorno_trabalho","mudanca_risco","demissional"}.Contains(input.Tipo)) throw new ArgumentException("Tipo de ASO inválido.");
  if(!new[]{"apto","inapto","apto_com_restricao","pendente"}.Contains(input.Resultado)) throw new ArgumentException("Resultado inválido.");
  if(input.Resultado=="apto_com_restricao"&&string.IsNullOrWhiteSpace(input.Restricao)) throw new ArgumentException("Descreva a restrição laboral.");
  var s=Scope(); return repository.SalvarAsoAsync(s.Tenant,s.Entidade,tenant.ExercicioId,id,input,user.UsuarioId,ct);
 }
}
