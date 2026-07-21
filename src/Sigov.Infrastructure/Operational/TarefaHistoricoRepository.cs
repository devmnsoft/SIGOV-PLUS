using System.Text.Json;
using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class TarefaHistoricoRepository : ITarefaHistoricoRepository { private readonly DapperContext _context; public TarefaHistoricoRepository(DapperContext context)=>_context=context; public async Task RegistrarAsync(long tarefaId,string acao,object? antes,object? depois,OperationalCommandContext context,CancellationToken cancellationToken){using var connection=_context.CreateConnection(); await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tarefa_historico (tenant_id, tarefa_id, acao, antes_json, depois_json, created_by, correlation_id, ip_address, user_agent) values (@TenantId, @TarefaId, @Acao, @Antes::jsonb, @Depois::jsonb, @UserId, @CorrelationId, @IpAddress, @UserAgent);", new { context.TenantId, TarefaId=tarefaId, Acao=acao, Antes=JsonSerializer.Serialize(antes), Depois=JsonSerializer.Serialize(depois), context.UserId, context.CorrelationId, context.IpAddress, context.UserAgent}, cancellationToken:cancellationToken)).ConfigureAwait(false);} }
