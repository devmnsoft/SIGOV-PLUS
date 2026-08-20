using Dapper;
using Npgsql;
using Sigov.Application.Saas.Context;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantContextSwitchRepository(DapperContext context) : ITenantContextSwitchRepository
{
    private const string ActiveAssignment = @"u.ativo and not u.is_deleted and ug.ativo and not ug.is_deleted
 and (ug.vigencia_inicio is null or ug.vigencia_inicio <= now()) and (ug.vigencia_fim is null or ug.vigencia_fim >= now())
 and gp.ativo and not gp.is_deleted and (gp.vigencia_inicio is null or gp.vigencia_inicio <= now()) and (gp.vigencia_fim is null or gp.vigencia_fim >= now())
 and pa.ativo and not pa.is_deleted";

    public async Task<IReadOnlyCollection<string>> GetUserProfileCodesAsync(long usuarioId, CancellationToken ct)
    {
        var sql = $@"select distinct 'ADMINISTRADOR_GERAL'
from sigov.usuario u join sigov.usuario_grupo ug on ug.usuario_id=u.id
join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id
join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.sistemico
join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR'
join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted and p.chave='contexto.empresa.assumir'
where u.id=@UsuarioId and {ActiveAssignment};";
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(sql, new { UsuarioId = usuarioId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<OperationalContext?> GetCurrentAsync(long usuarioId, string sessionHash, CancellationToken ct)
    {
        const string sql = @"select s.id SessionId,s.usuario_id UsuarioId,s.tenant_id TenantId,s.entidade_id EntidadeId,s.unidade_id UnidadeId,s.exercicio_id ExercicioId,s.sistema_id SistemaId,s.perfil_id PerfilId,s.modo_acesso ModoAcesso,s.contexto_global IsGlobal,s.versao Versao,s.expira_at ExpiresAt,t.nome EmpresaNome,uo.nome UnidadeNome,e.ano::text ExercicioNome,m.nome SistemaNome,m.rota_base RotaInicial
from sigov.sessao_contexto_usuario s join sigov.usuario u on u.id=s.usuario_id and u.ativo and not u.is_deleted
left join sigov.tenant t on t.id=s.tenant_id left join sigov.unidade_organizacional uo on uo.id=s.unidade_id
left join sigov.exercicio e on e.id=s.exercicio_id left join sigov.modulo_saas m on m.id=s.sistema_id
where s.usuario_id=@UsuarioId and s.chave_sessao_hash=@SessionHash and s.situacao='ATIVA' and s.expira_at>now();";
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OperationalContext>(new CommandDefinition(sql, new { UsuarioId = usuarioId, SessionHash = sessionHash }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ContextOption>> SearchTenantsAsync(long usuarioId, string? search, int offset, int limit, CancellationToken ct)
    {
        const string sql = @"select distinct t.id Id,t.slug Codigo,t.nome Nome,t.status Situacao
from sigov.tenant t
where t.ativo and not t.is_deleted and t.status not in ('CANCELADO','EXCLUIDO')
and (@Search is null or t.nome ilike '%'||@Search||'%' or t.slug ilike '%'||@Search||'%')
and (exists(select 1 from sigov.usuario_escopo_acesso ue where ue.usuario_id=@UsuarioId and ue.tenant_id=t.id and ue.ativo)
 or exists(select 1 from sigov.usuario u join sigov.usuario_grupo ug on ug.usuario_id=u.id join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.sistemico join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR' join sigov.permissao p on p.id=pp.permissao_id and p.chave='contexto.empresa.visualizar' and p.ativo and not p.is_deleted where u.id=@UsuarioId and u.ativo and not u.is_deleted and ug.ativo and gp.ativo))
order by Nome offset @Offset limit @Limit;";
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        var rows = await connection.QueryAsync<ContextOption>(new CommandDefinition(sql, new { UsuarioId = usuarioId, Search = string.IsNullOrWhiteSpace(search) ? null : search, Offset = offset, Limit = limit }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<IReadOnlyCollection<ContextOption>> GetOptionsAsync(long usuarioId, long tenantId, ContextOptionType type, CancellationToken ct)
    {
        var access = await SearchTenantsAsync(usuarioId, null, 0, 50, ct).ConfigureAwait(false);
        if (!access.Any(x => x.Id == tenantId)) return Array.Empty<ContextOption>();
        var sql = type switch
        {
            ContextOptionType.Unidade => @"select distinct uo.id Id,coalesce(uo.codigo_externo,uo.id::text) Codigo,uo.nome Nome,case when uo.ativo then 'ATIVA' else 'INATIVA' end Situacao,uo.entidade_id ParentId from sigov.usuario_escopo_acesso ue join sigov.unidade_organizacional uo on uo.entidade_id=ue.entidade_id and uo.ativo and not uo.is_deleted where ue.usuario_id=@UsuarioId and ue.tenant_id=@TenantId and ue.ativo and (ue.escopo in ('GLOBAL','TENANT','ENTIDADE') or ue.unidade_id=uo.id) order by Nome",
            ContextOptionType.Exercicio => @"select distinct e.id Id,e.ano::text Codigo,e.ano::text Nome,case when e.ativo and current_date between e.data_inicio and e.data_fim then 'ABERTO' else 'ENCERRADO' end Situacao,e.entidade_id ParentId from sigov.usuario_escopo_acesso ue join sigov.exercicio e on e.entidade_id=ue.entidade_id and e.ativo and not e.is_deleted where ue.usuario_id=@UsuarioId and ue.tenant_id=@TenantId and ue.ativo and (ue.exercicio_id is null or ue.exercicio_id=e.id) order by Nome desc",
            _ => @"select distinct m.id Id,m.codigo Codigo,m.nome Nome,'DISPONIVEL' Situacao,m.rota_base RotaInicial,m.icone Icone from sigov.tenant_modulo tm join sigov.modulo_saas m on m.id=tm.modulo_saas_id and m.ativo and not m.is_deleted where tm.tenant_id=@TenantId and tm.ativo and not tm.is_deleted and tm.habilitado and tm.contratado and tm.inicio_at<=now() and (tm.fim_at is null or tm.fim_at>=now()) and exists(select 1 from sigov.usuario_escopo_acesso ue where ue.usuario_id=@UsuarioId and ue.tenant_id=tm.tenant_id and ue.ativo and (ue.modulo_codigo is null or ue.modulo_codigo=m.codigo)) order by m.ordem,m.nome"
        };
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        var rows = await connection.QueryAsync<ContextOption>(new CommandDefinition(sql, new { UsuarioId = usuarioId, TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<ContextValidation> ValidateAsync(long usuarioId, ContextSelection s, CancellationToken ct)
    {
        const string sql = @"with perfil as (select pa.id,pa.sistemico from sigov.usuario u join sigov.usuario_grupo ug on ug.usuario_id=u.id and ug.ativo and not ug.is_deleted and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>=now()) join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>=now()) join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted where u.id=@UsuarioId and u.ativo and not u.is_deleted order by pa.sistemico desc limit 1), escopo as (select 1 from sigov.usuario_escopo_acesso ue where ue.usuario_id=@UsuarioId and ue.tenant_id=@TenantId and ue.entidade_id=@EntidadeId and ue.ativo and (ue.exercicio_id is null or ue.exercicio_id=@ExercicioId) and (ue.unidade_id is null or ue.unidade_id=@UnidadeId) and (ue.modulo_codigo is null or ue.modulo_codigo=(select codigo from sigov.modulo_saas where id=@SistemaId))), global as (select 1 from perfil pf join sigov.perfil_permissao pp on pp.perfil_acesso_id=pf.id and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR' join sigov.permissao p on p.id=pp.permissao_id and p.chave='contexto.empresa.assumir' and p.ativo and not p.is_deleted where pf.sistemico), valido as (select pf.id perfil_id,pf.sistemico from perfil pf join sigov.tenant t on t.id=@TenantId and t.ativo and not t.is_deleted and t.status not in ('CANCELADO','EXCLUIDO') join sigov.unidade_organizacional uo on uo.id=@UnidadeId and uo.entidade_id=@EntidadeId and uo.ativo and not uo.is_deleted join sigov.exercicio e on e.id=@ExercicioId and e.entidade_id=@EntidadeId and e.ativo and not e.is_deleted join sigov.tenant_modulo tm on tm.tenant_id=t.id and tm.modulo_saas_id=@SistemaId and tm.ativo and not tm.is_deleted and tm.habilitado and tm.contratado and tm.inicio_at<=now() and (tm.fim_at is null or tm.fim_at>=now()) where exists(select 1 from escopo) or exists(select 1 from global)) select perfil_id PerfilId,sistemico RequiresJustification from valido;";
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(long PerfilId, bool RequiresJustification)>(new CommandDefinition(sql, new { UsuarioId = usuarioId, s.TenantId, s.EntidadeId, s.UnidadeId, s.ExercicioId, s.SistemaId }, cancellationToken: ct)).ConfigureAwait(false);
        return row.PerfilId > 0 ? new(true, row.RequiresJustification, row.PerfilId, null, "Contexto válido.") : new(false, false, null, "contexto_nao_autorizado", "A combinação informada não está disponível.");
    }

    public Task<OperationalContext> ChangeAsync(ContextChange change, CancellationToken ct) => PersistAsync(change, false, ct);
    public async Task<OperationalContext> ReturnGlobalAsync(ContextChange change, CancellationToken ct)
    {
        if ((await GetUserProfileCodesAsync(change.UsuarioId, ct).ConfigureAwait(false)).Count == 0)
        {
            await RecordDeniedAsync(change, "RETORNO_GLOBAL_NEGADO", ct).ConfigureAwait(false);
            throw new InvalidOperationException("retorno_global_negado:Operação não autorizada.");
        }
        return await PersistAsync(change with { Selection = null }, true, ct).ConfigureAwait(false);
    }

    private async Task<OperationalContext> PersistAsync(ContextChange change, bool global, CancellationToken ct)
    {
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var current = await connection.QuerySingleOrDefaultAsync<(long Id, long Versao)>(new CommandDefinition("select id,versao from sigov.sessao_contexto_usuario where usuario_id=@UsuarioId and chave_sessao_hash=@SessionHash and situacao='ATIVA' for update", change, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (change.Selection?.Versao is long expected && current.Id > 0 && expected != current.Versao) throw new InvalidOperationException("contexto_concorrente:O contexto foi alterado em outra aba.");
        const string upsert = @"insert into sigov.sessao_contexto_usuario(usuario_id,chave_sessao_hash,tenant_id,entidade_id,unidade_id,exercicio_id,sistema_id,perfil_id,modo_acesso,contexto_global,situacao,expira_at) values(@UsuarioId,@SessionHash,@TenantId,@EntidadeId,@UnidadeId,@ExercicioId,@SistemaId,@PerfilId,@Modo,@Global,'ATIVA',@ExpiresAt) on conflict(chave_sessao_hash) do update set tenant_id=excluded.tenant_id,entidade_id=excluded.entidade_id,unidade_id=excluded.unidade_id,exercicio_id=excluded.exercicio_id,sistema_id=excluded.sistema_id,perfil_id=excluded.perfil_id,modo_acesso=excluded.modo_acesso,contexto_global=excluded.contexto_global,alterado_at=now(),expira_at=excluded.expira_at,versao=sigov.sessao_contexto_usuario.versao+1 where sigov.sessao_contexto_usuario.usuario_id=excluded.usuario_id returning id;";
        var s = change.Selection;
        var sessionId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(upsert, new { change.UsuarioId, change.SessionHash, TenantId=s?.TenantId, EntidadeId=s?.EntidadeId, UnidadeId=s?.UnidadeId, ExercicioId=s?.ExercicioId, SistemaId=s?.SistemaId, PerfilId=(await ResolveProfileAsync(connection, tx, change.UsuarioId, s, ct).ConfigureAwait(false)), Modo=s?.ModoAcesso ?? "GLOBAL", Global=global, change.ExpiresAt }, tx, cancellationToken: ct)).ConfigureAwait(false);
        const string audit = @"insert into sigov.contexto_operacional_auditoria(usuario_executor_id,sessao_contexto_id,tenant_novo_id,entidade_nova_id,unidade_nova_id,exercicio_novo_id,sistema_novo_id,modo_acesso,justificativa,resultado,correlation_id,ip,user_agent,origem) values(@UsuarioId,@SessionId,@TenantId,@EntidadeId,@UnidadeId,@ExercicioId,@SistemaId,@Modo,@Justificativa,'SUCESSO',@CorrelationId,@Ip,@UserAgent,'API');";
        await connection.ExecuteAsync(new CommandDefinition(audit, new { change.UsuarioId, SessionId=sessionId, TenantId=s?.TenantId, EntidadeId=s?.EntidadeId, UnidadeId=s?.UnidadeId, ExercicioId=s?.ExercicioId, SistemaId=s?.SistemaId, Modo=s?.ModoAcesso ?? "GLOBAL", Justificativa=s?.Justificativa?.Trim(), change.CorrelationId, change.Ip, change.UserAgent }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return (await GetCurrentAsync(change.UsuarioId, change.SessionHash, ct).ConfigureAwait(false))!;
    }

    private static async Task<long?> ResolveProfileAsync(NpgsqlConnection connection, NpgsqlTransaction tx, long userId, ContextSelection? selection, CancellationToken ct)
    {
        const string sql = "select pa.id from sigov.usuario_grupo ug join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted where ug.usuario_id=@UserId and ug.ativo and not ug.is_deleted order by pa.sistemico desc limit 1";
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { UserId=userId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task EndSessionAsync(long usuarioId, string sessionHash, string correlationId, string? ip, string? userAgent, CancellationToken ct)
    {
        const string sql = @"with ended as (update sigov.sessao_contexto_usuario set situacao='ENCERRADA',encerrado_at=now(),alterado_at=now(),versao=versao+1 where usuario_id=@UsuarioId and chave_sessao_hash=@SessionHash and situacao='ATIVA' returning id) insert into sigov.contexto_operacional_auditoria(usuario_executor_id,sessao_contexto_id,resultado,codigo_motivo,correlation_id,ip,user_agent,origem) select @UsuarioId,id,'SUCESSO','SESSAO_ENCERRADA',@CorrelationId,@Ip,@UserAgent,'API' from ended;";
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UsuarioId=usuarioId, SessionHash=sessionHash, CorrelationId=correlationId, Ip=ip, UserAgent=userAgent }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task RecordDeniedAsync(ContextChange change, string code, CancellationToken ct)
    {
        const string sql = @"insert into sigov.contexto_operacional_auditoria(usuario_executor_id,tenant_novo_id,entidade_nova_id,unidade_nova_id,exercicio_novo_id,sistema_novo_id,modo_acesso,resultado,codigo_motivo,correlation_id,ip,user_agent,origem) values(@UsuarioId,@TenantId,@EntidadeId,@UnidadeId,@ExercicioId,@SistemaId,@Modo,'NEGADO',@Code,@CorrelationId,@Ip,@UserAgent,'API')";
        var s = change.Selection;
        await using var connection = (NpgsqlConnection)context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { change.UsuarioId, TenantId=s?.TenantId, EntidadeId=s?.EntidadeId, UnidadeId=s?.UnidadeId, ExercicioId=s?.ExercicioId, SistemaId=s?.SistemaId, Modo=s?.ModoAcesso, Code=code[..Math.Min(code.Length,80)], change.CorrelationId, change.Ip, change.UserAgent }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<long> StartSwitchAsync(TenantContextSwitchRequest request, CancellationToken ct) { const string sql="insert into sigov.usuario_contexto_global_log(usuario_global_id,tenant_destino_id,entidade_destino_id,motivo,ip,user_agent,correlation_id) values(@UsuarioGlobalId,@TenantDestinoId,@EntidadeDestinoId,@Motivo,@Ip,@UserAgent,@CorrelationId) returning id"; await using var c=(NpgsqlConnection)context.CreateConnection(); return await c.ExecuteScalarAsync<long>(new CommandDefinition(sql,request,cancellationToken:ct)).ConfigureAwait(false); }
    public async Task FinishSwitchAsync(long logId,long usuarioGlobalId,CancellationToken ct) { await using var c=(NpgsqlConnection)context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition("update sigov.usuario_contexto_global_log set finalizado_at=now() where id=@LogId and usuario_global_id=@UsuarioGlobalId and finalizado_at is null",new{LogId=logId,UsuarioGlobalId=usuarioGlobalId},cancellationToken:ct)).ConfigureAwait(false); }
    public async Task<IReadOnlyCollection<TenantContextLogItem>> GetLogsAsync(long? usuarioGlobalId,long? tenantId,CancellationToken ct) { const string sql="select id Id,usuario_global_id UsuarioGlobalId,tenant_destino_id TenantDestinoId,entidade_destino_id EntidadeDestinoId,motivo Motivo,iniciado_at IniciadoAt,finalizado_at FinalizadoAt,ip Ip,user_agent UserAgent,correlation_id CorrelationId from sigov.usuario_contexto_global_log where (@UsuarioGlobalId is null or usuario_global_id=@UsuarioGlobalId) and (@TenantId is null or tenant_destino_id=@TenantId) order by iniciado_at desc limit 200"; await using var c=(NpgsqlConnection)context.CreateConnection(); var rows=await c.QueryAsync<TenantContextLogItem>(new CommandDefinition(sql,new{UsuarioGlobalId=usuarioGlobalId,TenantId=tenantId},cancellationToken:ct)).ConfigureAwait(false); return rows.AsList(); }
}
