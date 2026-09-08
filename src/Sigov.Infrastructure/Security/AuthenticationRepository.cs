using Dapper;
using Sigov.Application.Security;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Security;

public sealed class AuthenticationRepository(NpgsqlConnectionFactory connectionFactory) : IAuthenticationRepository
{
    public async Task<AuthenticationUser?> FindForLoginAsync(string loginOrEmail, CancellationToken cancellationToken)
    {
        var identifier = AuthenticationIdentifierNormalizer.Normalize(loginOrEmail);
        return (await FindLoginCandidatesAsync(identifier, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<AuthenticationUser>> FindLoginCandidatesAsync(AuthenticationIdentifier identifier, CancellationToken cancellationToken)
    {
        if (!identifier.IsValid) return Array.Empty<AuthenticationUser>();

        const string sql = @"select u.id, u.tenant_id as TenantId, coalesce(u.nome, u.login) as Nome, u.login, coalesce(u.email, '') as Email,
       coalesce(t.nome, '') as TenantName,
       u.senha_hash as PasswordHash, u.ativo, u.bloqueado, coalesce(u.deve_alterar_senha, false) as DeveAlterarSenha,
       u.is_deleted as IsDeleted, coalesce(t.ativo, true) as TenantAtivo,
       coalesce(t.is_deleted, false) as TenantIsDeleted, count(*) over()::integer as MatchingUsers
from sigov.usuario u
left join sigov.tenant t on t.id = u.tenant_id
where (@Kind = 'LegacyLogin' and lower(trim(u.login)) = @Value)
   or (@Kind = 'Email' and (lower(trim(u.email)) = @Value or lower(trim(u.login)) = @Value))
   or (@Kind = 'Cpf' and (
        lower(trim(u.login)) = @Value
        or exists (
            select 1 from sigov.pessoa_fisica pf
            where pf.pessoa_id = u.pessoa_id and pf.ativo and not pf.is_deleted
              and regexp_replace(pf.cpf, '[^0-9]', '', 'g') = @Value)))
   or (@Kind = 'Cnpj' and (
        lower(trim(u.login)) = @Value
        or regexp_replace(coalesce(t.documento, ''), '[^0-9]', '', 'g') = @Value
        or exists (
            select 1 from sigov.entidade e
            where e.id = u.entidade_id and e.ativo and not e.is_deleted
              and regexp_replace(e.cnpj, '[^0-9]', '', 'g') = @Value)))
order by u.is_deleted asc, u.ativo desc, u.bloqueado asc, coalesce(t.ativo, true) desc,
         coalesce(t.is_deleted, false) asc, u.id desc
limit 100;";
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<AuthenticationUser>(new CommandDefinition(sql, new
        {
            Kind = identifier.Kind.ToString(),
            identifier.Value
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<AuthenticationAccess> GetAccessAsync(long userId, CancellationToken cancellationToken)
        => await GetAccessCoreAsync(userId, null, null, null, false, cancellationToken).ConfigureAwait(false);

    public async Task<AuthenticationAccess> GetRequestAccessAsync(long userId, long? tenantId, long? entidadeId, long? exercicioId, CancellationToken cancellationToken)
        => await GetAccessCoreAsync(userId, tenantId, entidadeId, exercicioId, true, cancellationToken).ConfigureAwait(false);

    private async Task<AuthenticationAccess> GetAccessCoreAsync(long userId, long? tenantId, long? entidadeId, long? exercicioId, bool restrictContext, CancellationToken cancellationToken)
    {
        const string sql = @"select distinct access_value from (
 select pn.codigo as access_value from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id join sigov.perfil_nivel pn on pn.codigo=(case when upper(trim(u.tipo_usuario)) in ('SIGOV_ADMIN','SUPER_ADMIN','SUPERADMIN','ADMIN_GERAL','ADMINISTRADOR_GERAL') then 'ADMINISTRADOR_GERAL' else upper(trim(u.tipo_usuario)) end) and pn.ativo where u.id=@UserId and u.ativo and not u.bloqueado and not u.is_deleted and (u.tenant_id is null or (t.ativo and not t.is_deleted))
 union select pn.codigo from sigov.usuario_grupo ug join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted
 join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
 join sigov.perfil_nivel pn on pn.codigo=(case when upper(trim(pa.codigo_externo)) in ('SIGOV_ADMIN','SUPER_ADMIN','SUPERADMIN','ADMIN_GERAL','ADMINISTRADOR_GERAL') then 'ADMINISTRADOR_GERAL' else upper(trim(pa.codigo_externo)) end) and pn.ativo where ug.usuario_id=@UserId and ug.ativo and not ug.is_deleted
 and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>=now())
 and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>=now())
) roles where access_value is not null;
select distinct p.chave from sigov.usuario_grupo ug
 join sigov.usuario u on u.id=ug.usuario_id and u.ativo and not u.bloqueado and not u.is_deleted
 left join sigov.tenant t on t.id=u.tenant_id
 join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
 join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and gp.ativo and not gp.is_deleted
 join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
 join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR'
 join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
 where ug.usuario_id=@UserId and ug.ativo and not ug.is_deleted and (u.tenant_id is null or (t.ativo and not t.is_deleted))
 and (ug.vigencia_inicio is null or ug.vigencia_inicio<=now()) and (ug.vigencia_fim is null or ug.vigencia_fim>=now())
 and (gp.vigencia_inicio is null or gp.vigencia_inicio<=now()) and (gp.vigencia_fim is null or gp.vigencia_fim>=now())
 and (pp.vigencia_inicio is null or pp.vigencia_inicio<=now()) and (pp.vigencia_fim is null or pp.vigencia_fim>=now())
 and (not @RestrictContext or ((ug.tenant_id is null or ug.tenant_id=@TenantId) and (gp.tenant_id is null or gp.tenant_id=@TenantId) and (pp.tenant_id is null or pp.tenant_id=@TenantId)
   and (ug.entidade_id is null or ug.entidade_id=@EntidadeId) and (gp.entidade_id is null or gp.entidade_id=@EntidadeId) and (pp.entidade_id is null or pp.entidade_id=@EntidadeId)
   and (ug.exercicio_id is null or ug.exercicio_id=@ExercicioId) and (gp.exercicio_id is null or gp.exercicio_id=@ExercicioId) and (pp.exercicio_id is null or pp.exercicio_id=@ExercicioId)))
 and not exists (select 1 from sigov.perfil_permissao deny where deny.perfil_acesso_id=pa.id and deny.permissao_id=p.id
   and deny.ativo and not deny.is_deleted and deny.efeito='NEGAR'
   and (deny.vigencia_inicio is null or deny.vigencia_inicio<=now()) and (deny.vigencia_fim is null or deny.vigencia_fim>=now()));";
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { UserId = userId, TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, RestrictContext = restrictContext }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var roles = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(roles, permissions);
    }

    public async Task<AccountReference?> FindActiveAccountAsync(string loginOrEmail, CancellationToken cancellationToken)
    {
        var candidates = await FindLoginCandidatesAsync(AuthenticationIdentifierNormalizer.Normalize(loginOrEmail), cancellationToken).ConfigureAwait(false);
        var active = candidates.Where(candidate => candidate.Ativo && !candidate.Bloqueado && !candidate.IsDeleted &&
            candidate.TenantAtivo && !candidate.TenantIsDeleted && !string.IsNullOrWhiteSpace(candidate.Email)).ToArray();
        return active.Length == 1 ? new(active[0].Id, active[0].TenantId, active[0].Nome, active[0].Email) : null;
    }

    public async Task<bool> StorePasswordResetTokenAsync(AccountReference account, string tokenHash, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = @"with eligible as (
 select @UsuarioId::bigint as usuario_id where not exists (
   select 1 from sigov.senha_redefinicao_token where usuario_id=@UsuarioId and usado_at is null and created_at > now()-interval '60 seconds'
 )
), invalidate as (
 update sigov.senha_redefinicao_token set usado_at=now()
 where usuario_id=@UsuarioId and usado_at is null and exists(select 1 from eligible)
)
insert into sigov.senha_redefinicao_token(tenant_id, usuario_id, token_hash, expira_at, correlation_id)
select @TenantId,usuario_id,@TokenHash,now()+interval '30 minutes',@CorrelationId from eligible;";
        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(@UsuarioId)", new { UsuarioId = account.Id }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(sql, new { account.TenantId, UsuarioId = account.Id, TokenHash = tokenHash, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    public async Task RevokePasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.senha_redefinicao_token set usado_at=now() where token_hash=@TokenHash and usado_at is null";
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<AccountReference?> ConsumePasswordResetTokenAsync(string tokenHash, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = @"with valid_token as (
 update sigov.senha_redefinicao_token rt set usado_at=now()
 where rt.id=(select rt2.id from sigov.senha_redefinicao_token rt2
 join sigov.usuario u2 on u2.id=rt2.usuario_id and u2.ativo and not u2.bloqueado and not u2.is_deleted
 left join sigov.tenant t2 on t2.id=u2.tenant_id
 where rt2.token_hash=@TokenHash and rt2.usado_at is null and rt2.expira_at>now()
 and (u2.tenant_id is null or (t2.ativo and not t2.is_deleted)) order by rt2.created_at desc limit 1 for update skip locked)
 returning rt.id, rt.usuario_id, rt.tenant_id), invalidate_others as (
 update sigov.senha_redefinicao_token rt set usado_at=now() from valid_token t
 where rt.usuario_id=t.usuario_id and rt.id<>t.id and rt.usado_at is null)
update sigov.usuario u set senha_hash=@PasswordHash, deve_alterar_senha=false, updated_at=now()
from valid_token t where u.id=t.usuario_id and u.tenant_id is not distinct from t.tenant_id
returning u.id, u.tenant_id as TenantId;";
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AccountReference>(new CommandDefinition(sql, new { TokenHash = tokenHash, PasswordHash = passwordHash }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string?> GetCurrentPasswordHashAsync(long tenantId, long userId, CancellationToken cancellationToken)
    {
        const string sql = "select senha_hash from sigov.usuario where id=@UserId and tenant_id=@TenantId and ativo and not bloqueado and not is_deleted";
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> ChangePasswordAsync(long tenantId, long userId, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.usuario set senha_hash=@PasswordHash, deve_alterar_senha=false, updated_at=now() where id=@UserId and tenant_id=@TenantId and ativo and not bloqueado and not is_deleted";
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    private static bool IsSafeClaimValue(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 150;
}
