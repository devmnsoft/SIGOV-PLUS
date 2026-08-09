using Dapper;
using Sigov.Application.Ui;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Ui;

public sealed class UserPreferenceRepository
{
    private readonly DapperContext _context;

    public UserPreferenceRepository(DapperContext context) => _context = context;

    public async Task<UserPreferenceResponse?> GetAsync(long? tenantId, long userId, string key, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT tenant_id AS TenantId,
       usuario_id AS UserId,
       chave AS Key,
       valor::text AS ValueJson,
       COALESCE(updated_at, created_at) AS UpdatedAt
  FROM sigov.usuario_preferencia
 WHERE ((tenant_id IS NULL AND @TenantId IS NULL) OR tenant_id = @TenantId)
   AND usuario_id = @UserId
   AND chave = @Key
 LIMIT 1;
";

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserPreferenceResponse>(new CommandDefinition(sql,
            new { TenantId = tenantId, UserId = userId, Key = key }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<UserPreferenceResponse> UpsertAsync(UserPreferenceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"INSERT INTO sigov.usuario_preferencia (tenant_id, usuario_id, chave, valor)
VALUES (@TenantId, @UserId, @Key, CAST(@ValueJson AS jsonb))
ON CONFLICT (tenant_id, usuario_id, chave)
DO UPDATE SET valor = EXCLUDED.valor,
              updated_at = now()
RETURNING tenant_id AS TenantId,
          usuario_id AS UserId,
          chave AS Key,
          valor::text AS ValueJson,
          COALESCE(updated_at, created_at) AS UpdatedAt;
";

        cancellationToken.ThrowIfCancellationRequested();
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleAsync<UserPreferenceResponse>(new CommandDefinition(sql, request,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
