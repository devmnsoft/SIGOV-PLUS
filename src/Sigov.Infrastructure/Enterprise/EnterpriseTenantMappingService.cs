using Dapper;
using Sigov.Application.Enterprise;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Enterprise;

public sealed class EnterpriseTenantMappingService : IEnterpriseTenantMappingService
{
    private readonly DapperContext _context;

    public EnterpriseTenantMappingService(DapperContext context)
    {
        _context = context;
    }

    public async Task<Guid?> ResolveEnterpriseTenantAsync(long coreTenantId, CancellationToken cancellationToken)
    {
        const string sql = @"select enterprise_tenant_id
from sigov.enterprise_tenant_mapping
where core_tenant_id = @CoreTenantId and ativo = true
limit 1;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { CoreTenantId = coreTenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<long?> ResolveCoreTenantAsync(Guid enterpriseTenantId, CancellationToken cancellationToken)
    {
        if (enterpriseTenantId == Guid.Empty)
        {
            return null;
        }

        const string sql = @"select core_tenant_id
from sigov.enterprise_tenant_mapping
where enterprise_tenant_id = @EnterpriseTenantId and ativo = true
limit 1;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { EnterpriseTenantId = enterpriseTenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
