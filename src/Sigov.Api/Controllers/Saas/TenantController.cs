using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers.Saas;

[ApiController]
[Route("api/saas/tenant")]
public sealed class TenantController : ControllerBase
{
    private readonly ITenantContext _tenantContext;
    private readonly DapperContext _context;

    public TenantController(ITenantContext tenantContext, DapperContext context)
    {
        _tenantContext = tenantContext;
        _context = context;
    }

    [HttpGet("atual")]
    public ActionResult<ApiResponse<object>> Atual()
    {
        if (!_tenantContext.IsResolved)
        {
            return BadRequest(ApiResponse<object>.Fail("Tenant não resolvido."));
        }

        return Ok(ApiResponse<object>.Ok(new { _tenantContext.TenantId, _tenantContext.TenantSlug, _tenantContext.Status }));
    }

    [HttpGet("status")]
    public ActionResult<ApiResponse<object>> Status() => Ok(ApiResponse<object>.Ok(new { _tenantContext.TenantId, _tenantContext.Status }));

    [HttpGet("modulos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantModuleInfo>>>> Modulos(CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            return BadRequest(ApiResponse<IReadOnlyCollection<TenantModuleInfo>>.Fail("Tenant não resolvido."));
        }

        const string sql = """
            select ms.codigo, ms.nome, tm.contratado, tm.habilitado
            from sigov.tenant_modulo tm
            join sigov.modulo_saas ms on ms.id = tm.modulo_saas_id
            where tm.tenant_id = @TenantId and tm.ativo = true and ms.ativo = true
            order by ms.ordem, ms.nome;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantModuleInfo>(new CommandDefinition(sql, new { TenantId = _tenantContext.TenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantModuleInfo>>.Ok(rows.AsList()));
    }

    [HttpGet("features")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantFeatureInfo>>>> Features(CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            return BadRequest(ApiResponse<IReadOnlyCollection<TenantFeatureInfo>>.Fail("Tenant não resolvido."));
        }

        const string sql = """
            select ffd.codigo, tff.habilitado, tff.valor::text as ValorJson
            from sigov.tenant_feature_flag tff
            join sigov.feature_flag_def ffd on ffd.id = tff.feature_flag_def_id
            where tff.tenant_id = @TenantId and tff.ativo = true and ffd.ativo = true
            order by ffd.codigo;
            """;
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantFeatureInfo>(new CommandDefinition(sql, new { TenantId = _tenantContext.TenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantFeatureInfo>>.Ok(rows.AsList()));
    }
}
