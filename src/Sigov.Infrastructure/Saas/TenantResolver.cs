using Dapper;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class TenantResolver : ITenantResolver
{
    private readonly DapperContext _context;

    public TenantResolver(DapperContext context) => _context = context;

    public async Task<TenantResolutionResult> ResolveAsync(string? host, string? headerSlug, string? querySlug, IReadOnlyDictionary<string, string?> claims, bool allowDevelopmentResolvers, CancellationToken cancellationToken)
    {
        var slug = ResolveSlug(host, headerSlug, querySlug, claims, allowDevelopmentResolvers);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return new TenantResolutionResult(false, null, "Tenant não resolvido pelo host, header de desenvolvimento ou claim autenticada.");
        }

        const string sql = @"select id, nome, slug, status, ambiente
from sigov.tenant
where slug = @Slug and ativo = true and is_deleted = false
limit 1;
";
        using var connection = _context.CreateConnection();
        var tenant = await connection.QuerySingleOrDefaultAsync<TenantInfo>(new CommandDefinition(sql, new { Slug = slug }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return tenant is null
            ? new TenantResolutionResult(false, null, "Tenant informado não existe ou está inativo.")
            : new TenantResolutionResult(true, tenant, null);
    }

    private static string? ResolveSlug(string? host, string? headerSlug, string? querySlug, IReadOnlyDictionary<string, string?> claims, bool allowDevelopmentResolvers)
    {
        if (!string.IsNullOrWhiteSpace(host))
        {
            var hostname = host.Split(':', 2)[0].ToLowerInvariant();
            if (hostname.EndsWith(".sigov.local", StringComparison.OrdinalIgnoreCase) || hostname.EndsWith(".sigov.com.br", StringComparison.OrdinalIgnoreCase))
            {
                var firstLabel = hostname.Split('.', 2)[0];
                if (!string.Equals(firstLabel, "www", StringComparison.OrdinalIgnoreCase) && !string.Equals(firstLabel, "api", StringComparison.OrdinalIgnoreCase))
                {
                    return firstLabel;
                }
            }
        }

        if (allowDevelopmentResolvers && !string.IsNullOrWhiteSpace(headerSlug))
        {
            return headerSlug.Trim().ToLowerInvariant();
        }

        if (allowDevelopmentResolvers && !string.IsNullOrWhiteSpace(querySlug))
        {
            return querySlug.Trim().ToLowerInvariant();
        }

        if (claims.TryGetValue("tenant_slug", out var claimSlug) && !string.IsNullOrWhiteSpace(claimSlug))
        {
            return claimSlug.Trim().ToLowerInvariant();
        }

        return null;
    }
}
