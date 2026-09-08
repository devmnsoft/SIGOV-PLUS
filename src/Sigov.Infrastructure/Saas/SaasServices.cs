using Dapper;
using Sigov.Application.Saas;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly DapperContext _context;

    public FeatureFlagService(DapperContext context) => _context = context;

    public async Task<bool> IsEnabledAsync(long tenantId, string featureCode, CancellationToken cancellationToken)
    {
        const string sql = @"select exists (
    select 1
    from sigov.tenant_feature_flag tff
    join sigov.feature_flag_def ffd on ffd.id = tff.feature_flag_def_id
    where tff.tenant_id = @TenantId
      and ffd.codigo = @FeatureCode
      and tff.habilitado = true
      and tff.ativo = true
      and ffd.ativo = true
);
";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId, FeatureCode = featureCode }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class ModuloLicenciamentoService(IModuleAccessRepository repository, IModuleCatalogService catalog) : IModuloLicenciamentoService
{
    private static readonly ISet<string> EnabledStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CONTRATADO", "HABILITADO", "ATIVO", "TRIAL", "EM_IMPLANTACAO", "BETA"
    };

    public async Task<bool> IsModuleEnabledAsync(long tenantId, string moduleCode, CancellationToken cancellationToken)
    {
        var contract = await repository.GetTenantModuleAsync(tenantId, moduleCode, cancellationToken).ConfigureAwait(false);
        if (!IsEnabled(contract)) return false;

        var module = catalog.FindByCode(moduleCode);
        if (module is null) return false;
        foreach (var dependency in module.Dependencias)
        {
            if (!IsEnabled(await repository.GetTenantModuleAsync(tenantId, dependency, cancellationToken).ConfigureAwait(false)))
                return false;
        }
        return true;
    }

    private static bool IsEnabled(TenantModuleContract? contract)
    {
        if (contract is null || !contract.Active || !EnabledStatuses.Contains(contract.Status)) return false;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (!contract.EffectiveFrom.HasValue || contract.EffectiveFrom <= today) &&
               (!contract.EffectiveUntil.HasValue || contract.EffectiveUntil >= today);
    }
}

public sealed class TenantUsageMeter : ITenantUsageMeter
{
    private readonly DapperContext _context;

    public TenantUsageMeter(DapperContext context) => _context = context;

    public async Task RegistrarRequisicaoAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.tenant_uso_mensal (tenant_id, ano, mes, requisicoes_api)
values (@TenantId, extract(year from now())::int, extract(month from now())::int, 1)
on conflict (tenant_id, ano, mes)
do update set requisicoes_api = sigov.tenant_uso_mensal.requisicoes_api + 1, updated_at = now();
";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class TenantConfigurationProvider : ITenantConfigurationProvider
{
    private readonly DapperContext _context;

    public TenantConfigurationProvider(DapperContext context) => _context = context;

    public async Task<string?> ObterValorJsonAsync(long tenantId, string chave, CancellationToken cancellationToken)
    {
        const string sql = "select valor::text from sigov.tenant_configuracao where tenant_id = @TenantId and chave = @Chave and ativo = true limit 1;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { TenantId = tenantId, Chave = chave }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
