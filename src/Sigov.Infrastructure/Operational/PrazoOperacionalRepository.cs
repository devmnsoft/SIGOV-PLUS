using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class PrazoOperacionalRepository : IPrazoOperacionalRepository { private readonly DapperContext _context; public PrazoOperacionalRepository(DapperContext context)=>_context=context; public async Task<IReadOnlyList<PrazoOperacionalDto>> ListarVencidosAsync(long tenantId,DateTimeOffset referencia,CancellationToken cancellationToken){using var connection=_context.CreateConnection(); var rows=await connection.QueryAsync<PrazoOperacionalDto>(new CommandDefinition(@"select id, tenant_id as TenantId, titulo, vence_em as VenceEm, status, tarefa_id as TarefaId from sigov.prazo_operacional where tenant_id = @TenantId and status <> 'CONCLUIDO' and vence_em < @Referencia and is_deleted = false;", new {TenantId=tenantId, Referencia=referencia}, cancellationToken:cancellationToken)).ConfigureAwait(false); return rows.AsList();} }
