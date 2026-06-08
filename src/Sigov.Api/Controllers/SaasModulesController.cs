using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Modules;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas")]
public sealed class SaasModulesController : ControllerBase
{
    private readonly IModuleAccessRepository _accessRepository;
    private readonly IModuleCatalogService _catalogService;

    public SaasModulesController(IModuleCatalogService catalogService, IModuleAccessRepository accessRepository)
    {
        _catalogService = catalogService;
        _accessRepository = accessRepository;
    }

    [HttpGet("modulos")]
    public ActionResult<ApiResponse<IReadOnlyCollection<ModuleCatalogItem>>> GetModules() => Ok(ApiResponse<IReadOnlyCollection<ModuleCatalogItem>>.Ok(_catalogService.GetModules()));

    [HttpGet("modulos/{codigo}")]
    public ActionResult<ApiResponse<ModuleCatalogItem>> GetModule(string codigo)
    {
        var module = _catalogService.FindByCode(codigo);
        return module is null ? NotFound(ApiResponse<ModuleCatalogItem>.Fail("Módulo não encontrado.")) : Ok(ApiResponse<ModuleCatalogItem>.Ok(module));
    }

    [HttpGet("tenants/{tenantId:long}/modulos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantModuleContract>>>> GetTenantModules(long tenantId, CancellationToken cancellationToken)
    {
        var rows = await _accessRepository.GetTenantModulesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantModuleContract>>.Ok(rows));
    }

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/habilitar")]
    public Task<ActionResult<ApiResponse<object>>> Enable(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "HABILITADO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/suspender")]
    public Task<ActionResult<ApiResponse<object>>> Suspend(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "SUSPENSO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> Cancel(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "CANCELADO", cancellationToken);

    private async Task<ActionResult<ApiResponse<object>>> ChangeStatus(long tenantId, string codigo, string status, CancellationToken cancellationToken)
    {
        await _accessRepository.UpsertTenantModuleStatusAsync(tenantId, codigo, status, CurrentUserId(), CurrentCorrelationId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { tenantId, codigo, status }));
    }

    private long? CurrentUserId() => long.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("usuario_id")?.Value, out var id) ? id : null;

    private Guid? CurrentCorrelationId() => Guid.TryParse(HttpContext.TraceIdentifier, out var id) ? id : null;
}
