using Dapper;
using Sigov.Application.Security;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Security;

public sealed class AuthenticationRepository(NpgsqlConnectionFactory connectionFactory) : IAuthenticationRepository
{
    public async Task<AuthenticationUser?> FindForLoginAsync(string loginOrEmail, CancellationToken cancellationToken)
    {
        const string sql = @"select u.id, u.tenant_id as TenantId, coalesce(u.nome, u.login) as Nome, u.login, coalesce(u.email, '') as Email,
       coalesce(t.nome, '') as TenantName,
       u.senha_hash as PasswordHash, u.ativo, u.bloqueado, coalesce(u.deve_alterar_senha, false) as DeveAlterarSenha,
       u.is_deleted as IsDeleted, coalesce(t.ativo, true) as TenantAtivo,
       coalesce(t.is_deleted, false) as TenantIsDeleted, count(*) over()::integer as MatchingUsers
from sigov.usuario u
left join sigov.tenant t on t.id = u.tenant_id
where lower(u.login) = lower(@Value) or lower(u.email) = lower(@Value)
order by u.is_deleted asc, u.ativo desc, u.bloqueado asc, coalesce(t.ativo, true) desc,
         coalesce(t.is_deleted, false) asc, u.id desc
limit 1;";
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthenticationUser>(new CommandDefinition(sql, new { Value = loginOrEmail }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<AuthenticationAccess> GetAccessAsync(long userId, CancellationToken cancellationToken)
    {
        const string sql = @"select distinct access_value from (
 select pn.codigo as access_value from sigov.usuario u join sigov.perfil_nivel pn on pn.codigo=upper(trim(u.tipo_usuario)) and pn.ativo where u.id=@UserId
 union select pn.codigo from sigov.usuario_grupo ug join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and not gp.is_deleted
 join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
 join sigov.perfil_nivel pn on pn.codigo=upper(trim(pa.codigo_externo)) and pn.ativo where ug.usuario_id=@UserId and not ug.is_deleted
) roles where access_value is not null;
select distinct p.chave from sigov.usuario_grupo ug
 join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and not gp.is_deleted
 join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
 join sigov.perfil_permissao pp on pp.perfil_acesso_id=pa.id
 join sigov.permissao p on p.id=pp.permissao_id and p.ativo and not p.is_deleted
 where ug.usuario_id=@UserId and not ug.is_deleted;";
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var roles = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = (await result.ReadAsync<string>().ConfigureAwait(false)).Where(IsSafeClaimValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(roles, permissions);
    }

    public async Task<AccountReference?> FindActiveAccountAsync(string loginOrEmail, CancellationToken cancellationToken)
    {
        const string sql = @"select u.id, u.tenant_id as TenantId, coalesce(u.nome,u.login) as Nome, coalesce(u.email,'') as Email from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
where u.ativo and not u.bloqueado and not u.is_deleted and (u.tenant_id is null or (t.ativo and not t.is_deleted))
and (lower(u.login)=lower(@Value) or lower(u.email)=lower(@Value)) limit 1;";
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AccountReference>(new CommandDefinition(sql, new { Value = loginOrEmail }, cancellationToken: cancellationToken)).ConfigureAwait(false);
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
