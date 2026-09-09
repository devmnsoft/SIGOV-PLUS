using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Authentication;

public sealed class SigovApiAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SigovApi";

    public SigovApiAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase)
            && !IsPublic(Request.Path))
        {
            return await AuthenticateApiKeyAsync().ConfigureAwait(false);
        }

        return await AuthenticateBearerSessionAsync().ConfigureAwait(false);
    }

    private async Task<AuthenticateResult> AuthenticateApiKeyAsync()
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Header X-Api-Key obrigatorio.");
        }

        if (!long.TryParse(Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId) || tenantId <= 0)
        {
            return AuthenticateResult.Fail("Header X-Tenant-Id obrigatorio para API v1.");
        }

        const string sql = @"
select ak.id as Id, ak.tenant_id as TenantId, ak.api_key_hash as ApiKeyHash,
       coalesce(array_agg(ake.escopo) filter (
           where ake.escopo is not null
             and ake.is_deleted = false
             and ake.status = 'ATIVO'), array[]::text[]) as Scopes
  from sigov.api_key ak
  join sigov.tenant t on t.id = ak.tenant_id and t.ativo and not t.is_deleted
  left join sigov.api_key_escopo ake on ake.api_key_id = ak.id and ake.tenant_id = ak.tenant_id
 where ak.tenant_id = @TenantId
   and ak.is_deleted = false
   and ak.status = 'ATIVA'
   and ak.revoked_at is null
 group by ak.id, ak.tenant_id, ak.api_key_hash;";

        var db = Context.RequestServices.GetRequiredService<DapperContext>();
        using var connection = db.CreateConnection();
        var rows = await connection.QueryAsync<ApiKeyRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: Context.RequestAborted)).ConfigureAwait(false);
        var apiKeyHash = Hash(apiKey);
        var row = rows.FirstOrDefault(candidate => FixedEquals(apiKeyHash, candidate.ApiKeyHash));
        if (row is null)
        {
            return AuthenticateResult.Fail("API key ausente, revogada ou invalida.");
        }

        Context.Items["ApiKeyId"] = row.Id;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"api-key:{row.Id}"),
            new("api_key_id", row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("tenant_id", row.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        claims.AddRange(row.Scopes.Select(scope => new Claim("scope", scope)));
        return Success(claims);
    }

    private async Task<AuthenticateResult> AuthenticateBearerSessionAsync()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length < 32)
        {
            return AuthenticateResult.Fail("Bearer token invalido.");
        }

        const string sql = @"
select s.id as SessionId, s.usuario_id as UserId, s.tenant_id as TenantId, s.entidade_id as EntidadeId,
       s.exercicio_id as ExercicioId, s.auth_version as AuthVersion,
       coalesce(u.nome, u.login) as Nome, coalesce(u.email, '') as Email, u.login as Login,
       coalesce(t.nome, '') as TenantName
  from sigov.identidade_sessao s
  join sigov.usuario u on u.id = s.usuario_id and u.ativo and not u.bloqueado and not u.is_deleted
  join sigov.tenant t on t.id = s.tenant_id and t.ativo and not t.is_deleted
 where s.token_hash = @TokenHash
   and s.encerrada_at is null
   and s.expira_at > now()
   and coalesce(s.is_deleted, false) = false;";

        var db = Context.RequestServices.GetRequiredService<DapperContext>();
        using var connection = db.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(sql, new { TokenHash = Hash(token) }, cancellationToken: Context.RequestAborted)).ConfigureAwait(false);
        if (row is null)
        {
            return AuthenticateResult.Fail("Sessao ausente, revogada ou expirada.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, row.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, row.Nome),
            new(ClaimTypes.Email, row.Email),
            new("login", row.Login),
            new("session_id", row.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("tenant_id", row.TenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("auth_version", row.AuthVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        if (row.EntidadeId is not null) claims.Add(new("entidade_id", row.EntidadeId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (row.ExercicioId is not null) claims.Add(new("exercicio_id", row.ExercicioId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(row.TenantName)) claims.Add(new("tenant_name", row.TenantName));
        return Success(claims);
    }

    private static bool IsPublic(PathString path) =>
        path.StartsWithSegments("/api/v1/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);

    private static AuthenticateResult Success(IEnumerable<Claim> claims)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool FixedEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private sealed record ApiKeyRow(long Id, long TenantId, string ApiKeyHash, string[] Scopes);

    private sealed record SessionRow(long SessionId, long UserId, long TenantId, long? EntidadeId, long? ExercicioId, long AuthVersion, string Nome, string Email, string Login, string TenantName);
}
