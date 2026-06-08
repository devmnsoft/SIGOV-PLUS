using Dapper;
using Sigov.Application.Saas.Context;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantContextSwitchRepository : ITenantContextSwitchRepository
{
    private readonly DapperContext _context;

    public TenantContextSwitchRepository(DapperContext context) => _context = context;

    public async Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken cancellationToken)
    {
        const string sql = """
            select distinct codigo from (
                select coalesce(pn.codigo, pa.codigo_externo, upper(replace(pa.nome, ' ', '_'))) as codigo
                from sigov.usuario u
                left join sigov.usuario_grupo ug on ug.usuario_id = u.id and ug.is_deleted = false
                left join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and gp.is_deleted = false
                left join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.is_deleted = false and pa.ativo = true
                left join sigov.perfil_nivel pn on pn.codigo = coalesce(pa.codigo_externo, upper(replace(pa.nome, ' ', '_')))
                where u.id = @UsuarioId and u.ativo = true and u.is_deleted = false
                union all
                select case when tipo_usuario in ('SIGOV_ADMIN','SUPER_ADMIN','ADMINISTRADOR_GERAL') then 'ADMINISTRADOR_GERAL' else tipo_usuario end
                from sigov.usuario where id = @UsuarioId and tipo_usuario is not null
            ) p where codigo is not null;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.usuario_contexto_global_log (usuario_global_id, tenant_destino_id, entidade_destino_id, motivo, ip, user_agent, correlation_id)
            values (@UsuarioGlobalId, @TenantDestinoId, @EntidadeDestinoId, @Motivo, @Ip, @UserAgent, @CorrelationId)
            returning id;
            """;
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, request, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task FinishSwitchAsync(long logId, long usuarioGlobalId, CancellationToken cancellationToken)
    {
        const string sql = """
            update sigov.usuario_contexto_global_log
            set finalizado_at = now()
            where id = @LogId and usuario_global_id = @UsuarioGlobalId and finalizado_at is null;
            """;
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { LogId = logId, UsuarioGlobalId = usuarioGlobalId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId, long? tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id as Id, usuario_global_id as UsuarioGlobalId, tenant_destino_id as TenantDestinoId, entidade_destino_id as EntidadeDestinoId,
                   motivo as Motivo, iniciado_at as IniciadoAt, finalizado_at as FinalizadoAt, ip as Ip, user_agent as UserAgent, correlation_id as CorrelationId
            from sigov.usuario_contexto_global_log
            where (@UsuarioGlobalId is null or usuario_global_id = @UsuarioGlobalId)
              and (@TenantId is null or tenant_destino_id = @TenantId)
            order by iniciado_at desc
            limit 200;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantContextLogItem>(new CommandDefinition(sql, new { UsuarioGlobalId = usuarioGlobalId, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }
}
