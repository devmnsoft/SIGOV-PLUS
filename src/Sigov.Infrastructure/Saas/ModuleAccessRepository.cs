using Dapper;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class ModuleAccessRepository : IModuleAccessRepository
{
    private readonly DapperContext _context;

    public ModuleAccessRepository(DapperContext context) => _context = context;

    public async Task<TenantModuleContract?> GetTenantModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken)
    {
        const string sql = """
            select tenant_id as TenantId, modulo_codigo as ModuleCode, pacote_codigo as PackageCode, status as Status, ativo as Active
            from sigov.tenant_modulo_contratado
            where tenant_id = @TenantId and modulo_codigo = @ModuleCode and ativo = true
            limit 1;
            """;
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantModuleContract>(new CommandDefinition(sql, new { TenantId = tenantId, ModuleCode = moduleCode }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<TenantModuleContract>> GetTenantModulesAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            select tenant_id as TenantId, modulo_codigo as ModuleCode, pacote_codigo as PackageCode, status as Status, ativo as Active
            from sigov.tenant_modulo_contratado
            where tenant_id = @TenantId and ativo = true
            order by modulo_codigo;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantModuleContract>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.AsList();
    }

    public async Task<bool> IsFeatureEnabledAsync(long tenantId, string moduleCode, string featureCode, CancellationToken cancellationToken)
    {
        const string sql = """
            select exists (
                select 1
                from sigov.tenant_feature_flag
                where tenant_id = @TenantId
                  and modulo_codigo = @ModuleCode
                  and feature_codigo = @FeatureCode
                  and habilitada = true
            );
            """;
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId, ModuleCode = moduleCode, FeatureCode = featureCode }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpsertTenantModuleStatusAsync(long tenantId, string moduleCode, string status, long? userId, Guid? correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into sigov.tenant_modulo_contratado (tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ativo, created_by, correlation_id)
            values (@TenantId, @ModuleCode, @Status, current_date, current_date, true, @UserId, @CorrelationId)
            on conflict (tenant_id, modulo_codigo)
            do update set status = excluded.status, ativo = true, updated_at = now(), updated_by = @UserId, correlation_id = @CorrelationId;
            """;
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ModuleCode = moduleCode, Status = status, UserId = userId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
