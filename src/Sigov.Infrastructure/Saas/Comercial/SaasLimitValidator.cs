using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasLimitValidator : ISaasLimitValidator
{
    private readonly DapperContext _context;

    public SaasLimitValidator(DapperContext context) => _context = context;

    public async Task<SaasLimitValidationResult> ValidateUserLimitAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        var allowed = usage.LimiteUsuarios is null || usage.UsuariosAtivos < usage.LimiteUsuarios;
        return new SaasLimitValidationResult(allowed, allowed ? null : "Limite de usuários do plano atingido.", usage);
    }

    public async Task<SaasLimitValidationResult> ValidateModuleLimitAsync(long tenantId, string moduloCodigo, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"select exists(
select 1 from sigov.saas_assinatura a
join sigov.saas_plano_modulo pm on pm.plano_id=a.plano_id and pm.incluso=true
where a.tenant_id=@TenantId and a.status='ATIVA' and pm.modulo_codigo=@Modulo)", new { TenantId = tenantId, Modulo = moduloCodigo.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken));
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        return new SaasLimitValidationResult(allowed, allowed ? null : "Módulo não contratado no plano atual.", usage);
    }

    public async Task<SaasLimitValidationResult> ValidateWhiteLabelAllowedAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        return new SaasLimitValidationResult(usage.WhiteLabelPermitido, usage.WhiteLabelPermitido ? null : "Plano não permite white label.", usage);
    }

    public async Task<SaasUsageSummary> GetUsageSummaryAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<UsageRow>(new CommandDefinition(@"select @TenantId as TenantId, p.codigo as Plano, p.limite_usuarios as LimiteUsuarios,
coalesce((select count(*)::int from sigov.usuario u where u.tenant_id=@TenantId and u.ativo=true and u.is_deleted=false),0) as UsuariosAtivos,
coalesce((select count(*)::int from sigov.saas_assinatura_modulo m where m.tenant_id=@TenantId and m.habilitado=true),0) as ModulosAtivos,
null::int as LimiteModulos, p.permite_white_label as WhiteLabelPermitido, p.permite_dominio_customizado as DominioCustomizadoPermitido
from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
where a.tenant_id=@TenantId and a.status='ATIVA' order by a.created_at desc limit 1", new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (row is null) return new SaasUsageSummary(tenantId, null, 0, null, 0, null, false, false, 0);
        var percentual = row.LimiteUsuarios is null or 0 ? 0 : Math.Round((decimal)row.UsuariosAtivos * 100 / row.LimiteUsuarios.Value, 2);
        return new SaasUsageSummary(tenantId, row.Plano, row.UsuariosAtivos, row.LimiteUsuarios, row.ModulosAtivos, row.LimiteModulos, row.WhiteLabelPermitido, row.DominioCustomizadoPermitido, percentual);
    }

    private sealed record UsageRow(long TenantId, string? Plano, int UsuariosAtivos, int? LimiteUsuarios, int ModulosAtivos, int? LimiteModulos, bool WhiteLabelPermitido, bool DominioCustomizadoPermitido);
}
