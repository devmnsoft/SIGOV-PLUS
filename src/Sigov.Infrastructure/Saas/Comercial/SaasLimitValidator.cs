using System.Data;
using Dapper;
using Sigov.Application.Saas.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.Comercial;

public sealed class SaasLimitValidator : ISaasLimitValidator
{
    private readonly DapperContext _context;

    public SaasLimitValidator(DapperContext context) => _context = context;

    // Limite efetivo de usuários (RC-SAAS-AUT Etapa B): limite do plano + bônus dos addons
    // ATIVOS de tipo USUARIOS% (quantidade contratada x valor_quantidade do catálogo).
    // Plano ilimitado (limite_usuarios null) permanece ilimitado.
    private const string UsoBase = @"select @TenantId as TenantId, p.codigo as Plano,
case when p.limite_usuarios is null then null::int else p.limite_usuarios + coalesce((select sum(coalesce(aa.quantidade,1) * coalesce(add.valor_quantidade,1))::int
from sigov.saas_assinatura_addon aa join sigov.saas_addon add on add.codigo=aa.addon_codigo and add.ativo
where aa.assinatura_id=a.id and upper(aa.status)='ATIVO' and add.tipo_addon like 'USUARIOS%'),0) end as LimiteUsuarios,
coalesce((select count(*)::int from sigov.usuario u where u.tenant_id=@TenantId and u.ativo=true and u.is_deleted=false),0) as UsuariosAtivos,
coalesce((select count(*)::int from sigov.saas_assinatura_modulo m where m.tenant_id=@TenantId and m.habilitado=true),0) as ModulosAtivos,
null::int as LimiteModulos, p.permite_white_label as WhiteLabelPermitido, p.permite_dominio_customizado as DominioCustomizadoPermitido
from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
where a.tenant_id=@TenantId and a.status='ATIVA' order by a.created_at desc limit 1";

    private static string UsoComLock => UsoBase + " for update of a";

    public async Task<SaasLimitValidationResult> ValidateUserLimitAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageSummaryAsync(tenantId, cancellationToken);
        var allowed = usage.LimiteUsuarios is null || usage.UsuariosAtivos < usage.LimiteUsuarios;
        return new SaasLimitValidationResult(allowed, allowed ? null : "Limite de usuários do plano atingido.", usage);
    }

    /// <summary>
    /// Validação transacional do limite de usuários: trava a assinatura ativa (<c>for update</c>)
    /// para serializar criações concorrentes no mesmo tenant (critério G).
    /// </summary>
    public async Task<SaasLimitValidationResult> ValidateUserLimitTxAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, CancellationToken cancellationToken = default)
    {
        var row = await connection.QuerySingleOrDefaultAsync<UsageRow>(new CommandDefinition(UsoComLock, new { TenantId = tenantId }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var usage = row is null
            ? new SaasUsageSummary(tenantId, null, 0, null, 0, null, false, false, 0)
            : new SaasUsageSummary(tenantId, row.Plano, row.UsuariosAtivos, row.LimiteUsuarios, row.ModulosAtivos, row.LimiteModulos, row.WhiteLabelPermitido, row.DominioCustomizadoPermitido, Percentual(row));
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
        var row = await connection.QuerySingleOrDefaultAsync<UsageRow>(new CommandDefinition(UsoBase, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (row is null) return new SaasUsageSummary(tenantId, null, 0, null, 0, null, false, false, 0);
        return new SaasUsageSummary(tenantId, row.Plano, row.UsuariosAtivos, row.LimiteUsuarios, row.ModulosAtivos, row.LimiteModulos, row.WhiteLabelPermitido, row.DominioCustomizadoPermitido, Percentual(row));
    }

    private static decimal Percentual(UsageRow row) =>
        row.LimiteUsuarios is null or 0 ? 0 : Math.Round((decimal)row.UsuariosAtivos * 100 / row.LimiteUsuarios.Value, 2);

    private sealed record UsageRow(long TenantId, string? Plano, int? LimiteUsuarios, int UsuariosAtivos, int ModulosAtivos, int? LimiteModulos, bool WhiteLabelPermitido, bool DominioCustomizadoPermitido);
}
