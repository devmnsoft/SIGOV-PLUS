using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.WhiteLabel;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasWhiteLabelController : ControllerBase
{
    private readonly ITenantBrandingService _brandingService;
    private readonly ITenantDominioService _dominioService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    public SaasWhiteLabelController(ITenantBrandingService brandingService, ITenantDominioService dominioService, ICurrentTenant currentTenant, ICurrentUser currentUser) { _brandingService = brandingService; _dominioService = dominioService; _currentTenant = currentTenant; _currentUser = currentUser; }

    [HttpGet("api/tenant/branding")]
    public async Task<ActionResult<ApiResponse<TenantBrandingResponse>>> Branding(CancellationToken cancellationToken) => Ok(ApiResponse<TenantBrandingResponse>.Ok(await _brandingService.GetAsync(_currentTenant.TenantId ?? 0, cancellationToken).ConfigureAwait(false)));

    [HttpPut("api/tenant/branding")]
    public async Task<ActionResult<ApiResponse<TenantBrandingResponse>>> AtualizarBranding([FromBody] TenantBrandingUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _brandingService.UpdateAsync(_currentTenant.TenantId ?? 0, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<TenantBrandingResponse>.Fail(result.Error ?? "Branding inválido.")) : Ok(ApiResponse<TenantBrandingResponse>.Ok(result.Value!));
    }

    [HttpGet("api/tenant/dominios")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantDominioResponse>>>> Dominios(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<TenantDominioResponse>>.Ok(await _dominioService.ListAsync(_currentTenant.TenantId ?? 0, cancellationToken).ConfigureAwait(false)));

    [HttpPost("api/tenant/dominios")]
    public async Task<ActionResult<ApiResponse<TenantDominioResponse>>> CriarDominio([FromBody] TenantDominioCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _dominioService.CreateAsync(_currentTenant.TenantId ?? 0, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<TenantDominioResponse>.Fail(result.Error ?? "Domínio inválido.")) : Ok(ApiResponse<TenantDominioResponse>.Ok(result.Value!));
    }

    [HttpPost("api/tenant/dominios/{id:long}/verificar")]
    public async Task<ActionResult<ApiResponse<TenantDominioResponse>>> Verificar(long id, [FromBody] VerificarDominioRequest request, CancellationToken cancellationToken)
    {
        var result = await _dominioService.VerificarAsync(_currentTenant.TenantId ?? 0, id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<TenantDominioResponse>.Fail(result.Error ?? "Domínio inválido.")) : Ok(ApiResponse<TenantDominioResponse>.Ok(result.Value!));
    }
}
