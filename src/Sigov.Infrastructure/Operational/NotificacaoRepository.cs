using Dapper;
using Sigov.Application.Operational;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Operational;

public sealed class NotificacaoRepository : INotificacaoRepository, ITarefaNotificationService
{
    private readonly DapperContext _context;

    public NotificacaoRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyList<NotificacaoDto>> ListarAsync(long tenantId, long usuarioId, bool? lida, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<NotificacaoDto>(new CommandDefinition(@"
select id, tenant_id as TenantId, usuario_id as UsuarioId, tipo, titulo, lida, created_at as CreatedAt
from sigov.notificacao_usuario
where tenant_id = @TenantId and usuario_id = @UsuarioId and (@Lida is null or lida = @Lida) and is_deleted = false
order by created_at desc;", new { TenantId = tenantId, UsuarioId = usuarioId, Lida = lida }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task MarcarLidaAsync(long tenantId, long usuarioId, long notificacaoId, string correlationId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
update sigov.notificacao_usuario
set lida = true, lida_em = coalesce(lida_em, now()), updated_at = now(), correlation_id = @CorrelationId
where tenant_id = @TenantId and usuario_id = @UsuarioId and id = @NotificacaoId and is_deleted = false;", new { TenantId = tenantId, UsuarioId = usuarioId, NotificacaoId = notificacaoId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (affected == 0) throw new InvalidOperationException("Notificação não encontrada para o usuário e tenant informados.");
    }

    public async Task NotificarAsync(long tarefaId, string tipo, OperationalCommandContext context, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"
with tarefa_notificada as (
    select id, tenant_id, titulo, responsavel_id
    from sigov.tarefa
    where tenant_id = @TenantId and id = @TarefaId and is_deleted = false
), notificacao_criada as (
    insert into sigov.notificacao (tenant_id, tipo, titulo, mensagem, modulo, prioridade, origem, entidade, entidade_id, created_by, updated_by, correlation_id)
    select tenant_id, @Tipo, titulo, 'Atualização operacional da tarefa', 'operacional', 'NORMAL', 'tarefa', 'tarefa', id::text, @UserId, @UserId, @CorrelationId
    from tarefa_notificada
    returning id, tenant_id, tipo, titulo, created_by, correlation_id
)
insert into sigov.notificacao_usuario (tenant_id, notificacao_id, usuario_id, tipo, titulo, created_by, correlation_id)
select n.tenant_id, n.id, coalesce(t.responsavel_id, @UserId), n.tipo, n.titulo, n.created_by, n.correlation_id
from notificacao_criada n
join tarefa_notificada t on t.tenant_id = n.tenant_id
where not exists (
    select 1 from sigov.notificacao_preferencia p
    where p.tenant_id = n.tenant_id and p.usuario_id = coalesce(t.responsavel_id, @UserId) and p.tipo = n.tipo and p.habilitada = false
);", new { context.TenantId, TarefaId = tarefaId, Tipo = tipo, context.UserId, context.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
