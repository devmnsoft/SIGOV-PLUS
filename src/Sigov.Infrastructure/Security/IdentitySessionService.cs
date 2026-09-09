using System.Security.Cryptography;
using System.Text;
using Dapper;
using Sigov.Application.Security;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Security;

public sealed class IdentitySessionService(DapperContext context) : IIdentitySessionService
{
    public async Task<IssuedIdentitySession> CreateAsync(AuthenticationUser user, TimeSpan lifetime, string? ipAddress, string? userAgent, string correlationId, CancellationToken cancellationToken)
    {
        if (user.TenantId is null || user.TenantId <= 0)
            throw new InvalidOperationException("Sessao persistente exige tenant_id resolvido.");
        if (user.EntidadeId is null || user.EntidadeId <= 0)
            throw new InvalidOperationException("Sessao persistente exige entidade_id resolvida.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var tokenHash = Hash(token);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var authVersion = await ResolveAuthVersionAsync(user.Id, cancellationToken).ConfigureAwait(false);

        const string sql = @"
insert into sigov.identidade_sessao
    (tenant_id, entidade_id, exercicio_id, usuario_id, token_hash, expira_at, auth_version,
     ip_hash, user_agent_sanitizado, correlation_id, created_by)
values
    (@TenantId, @EntidadeId, @ExercicioId, @UserId, @TokenHash, @ExpiresAt, @AuthVersion,
     @IpHash, @UserAgent, @CorrelationId::uuid, @UserId)
returning id;";

        using var connection = context.CreateConnection();
        var sessionId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            TenantId = user.TenantId.Value,
            EntidadeId = user.EntidadeId.Value,
            ExercicioId = (long?)null,
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            AuthVersion = authVersion,
            IpHash = string.IsNullOrWhiteSpace(ipAddress) ? null : Hash(ipAddress),
            UserAgent = SanitizeUserAgent(userAgent),
            CorrelationId = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid()
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new IssuedIdentitySession(sessionId, token, authVersion, expiresAt);
    }

    public async Task<IdentitySessionValidation?> ValidateAsync(long sessionId, long userId, long tenantId, string token, long authVersion, CancellationToken cancellationToken)
    {
        if (sessionId <= 0 || userId <= 0 || tenantId <= 0 || string.IsNullOrWhiteSpace(token))
            return null;

        const string sql = @"
update sigov.identidade_sessao s
   set ultimo_acesso_at = now()
 where s.id = @SessionId
   and s.usuario_id = @UserId
   and s.tenant_id = @TenantId
   and s.token_hash = @TokenHash
   and s.auth_version = @AuthVersion
   and s.encerrada_at is null
   and s.expira_at > now()
   and coalesce(s.is_deleted, false) = false
 returning true as Valid, s.id as SessionId, s.usuario_id as UserId, s.tenant_id as TenantId,
           s.entidade_id as EntidadeId, s.exercicio_id as ExercicioId, s.auth_version as AuthVersion;";

        using var connection = context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<IdentitySessionValidation>(new CommandDefinition(sql, new
        {
            SessionId = sessionId,
            UserId = userId,
            TenantId = tenantId,
            TokenHash = Hash(token),
            AuthVersion = authVersion
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAsync(long sessionId, long userId, string reason, CancellationToken cancellationToken)
    {
        const string sql = @"
update sigov.identidade_sessao
   set encerrada_at = coalesce(encerrada_at, now()),
       revogada_at = coalesce(revogada_at, now()),
       motivo_encerramento = left(@Reason, 80),
       updated_at = now(),
       updated_by = @UserId
 where id = @SessionId and usuario_id = @UserId and encerrada_at is null;";
        using var connection = context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { SessionId = sessionId, UserId = userId, Reason = reason }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        const string sql = @"
update sigov.identidade_sessao
   set encerrada_at = coalesce(encerrada_at, now()),
       revogada_at = coalesce(revogada_at, now()),
       motivo_encerramento = left(@Reason, 80),
       updated_at = now(),
       updated_by = @UserId
 where usuario_id = @UserId and encerrada_at is null;";
        using var connection = context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, Reason = reason }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<long> ResolveAuthVersionAsync(long userId, CancellationToken cancellationToken)
    {
        const string sql = @"
select greatest(
    floor(extract(epoch from coalesce(u.updated_at, u.created_at, now())))::bigint,
    coalesce(max(floor(extract(epoch from coalesce(ug.created_at, now())))::bigint), 1),
    coalesce(max(floor(extract(epoch from coalesce(gp.created_at, now())))::bigint), 1),
    coalesce(max(floor(extract(epoch from coalesce(pp.created_at, now())))::bigint), 1),
    coalesce(max(floor(extract(epoch from coalesce(pa.updated_at, pa.created_at, now())))::bigint), 1),
    coalesce(max(floor(extract(epoch from coalesce(p.updated_at, p.created_at, now())))::bigint), 1)
)
from sigov.usuario u
left join sigov.usuario_grupo ug on ug.usuario_id = u.id
left join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id
left join sigov.perfil_permissao pp on pp.perfil_acesso_id = gp.perfil_acesso_id
left join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id
left join sigov.permissao p on p.id = pp.permissao_id
where u.id = @UserId
group by u.id, u.updated_at, u.created_at;";
        using var connection = context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string? SanitizeUserAgent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= 200 ? value : value[..200];
    }
}
