using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Comercial;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasAssinaturasController : ControllerBase
{
    private readonly ISaasAssinaturaService _service;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    public SaasAssinaturasController(ISaasAssinaturaService service, ICurrentUser currentUser, ICurrentTenant currentTenant) { _service = service; _currentUser = currentUser; _currentTenant = currentTenant; }

    [HttpGet("api/saas/assinaturas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasAssinaturaResponse>>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => Ok(ApiResponse<IReadOnlyCollection<SaasAssinaturaResponse>>.Ok(await _service.ListAdminAsync(page, pageSize, cancellationToken).ConfigureAwait(false)));

    [HttpGet("api/saas/assinaturas/{id:long}")]
    public async Task<ActionResult<ApiResponse<SaasAssinaturaResponse>>> Obter(long id, CancellationToken cancellationToken)
    {
        var row = await _service.GetAdminAsync(id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<SaasAssinaturaResponse>.Fail("Assinatura não encontrada.")) : Ok(ApiResponse<SaasAssinaturaResponse>.Ok(row));
    }

    [HttpPut("api/saas/assinaturas/{id:long}")]
    public async Task<ActionResult<ApiResponse<SaasAssinaturaResponse>>> Atualizar(long id, [FromBody] SaasAssinaturaUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<SaasAssinaturaResponse>.Fail(result.Error ?? "Assinatura inválida.")) : Ok(ApiResponse<SaasAssinaturaResponse>.Ok(result.Value!));
    }

    [HttpPost("api/saas/assinaturas/{id:long}/suspender")]
    public Task<ActionResult<ApiResponse<string>>> Suspender(long id, CancellationToken cancellationToken) => Status(id, "SUSPENSA", cancellationToken);
    [HttpPost("api/saas/assinaturas/{id:long}/reativar")]
    public Task<ActionResult<ApiResponse<string>>> Reativar(long id, CancellationToken cancellationToken) => Status(id, "ATIVA", cancellationToken);
    [HttpPost("api/saas/assinaturas/{id:long}/cancelar")]
    public Task<ActionResult<ApiResponse<string>>> Cancelar(long id, CancellationToken cancellationToken) => Status(id, "CANCELADA", cancellationToken);

    [HttpGet("api/tenant/minha-assinatura")]
    public async Task<ActionResult<ApiResponse<SaasAssinaturaResponse>>> Minha(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenant.TenantId ?? 0;
        var row = await _service.GetMinhaAssinaturaAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<SaasAssinaturaResponse>.Fail("Assinatura do tenant não encontrada.")) : Ok(ApiResponse<SaasAssinaturaResponse>.Ok(row));
    }

    [HttpGet("api/tenant/meus-modulos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<string>>>> Modulos(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<string>>.Ok(await _service.GetMeusModulosAsync(_currentTenant.TenantId ?? 0, cancellationToken).ConfigureAwait(false)));

    [HttpGet("api/tenant/meus-limites")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPlanoLimiteResponse>>>> Limites(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<SaasPlanoLimiteResponse>>.Ok(await _service.GetMeusLimitesAsync(_currentTenant.TenantId ?? 0, cancellationToken).ConfigureAwait(false)));

    private async Task<ActionResult<ApiResponse<string>>> Status(long id, string status, CancellationToken cancellationToken)
    {
        var result = await _service.ChangeStatusAsync(id, status, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<string>.Fail(result.Error ?? "Status inválido.")) : Ok(ApiResponse<string>.Ok(status));
    }
}
