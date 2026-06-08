using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Perfis;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasPerfilTemplatesController : ControllerBase
{
    private readonly ISaasPerfilTemplateService _service;
    private readonly ICurrentUser _currentUser;
    public SaasPerfilTemplatesController(ISaasPerfilTemplateService service, ICurrentUser currentUser) { _service = service; _currentUser = currentUser; }

    [HttpGet("api/saas/perfis-templates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPerfilTemplateResponse>>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => Ok(ApiResponse<IReadOnlyCollection<SaasPerfilTemplateResponse>>.Ok(await _service.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false)));

    [HttpPost("api/saas/perfis-templates")]
    public async Task<ActionResult<ApiResponse<SaasPerfilTemplateResponse>>> Criar([FromBody] SaasPerfilTemplateResponse request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<SaasPerfilTemplateResponse>.Fail(result.Error ?? "Template inválido.")) : Ok(ApiResponse<SaasPerfilTemplateResponse>.Ok(result.Value!));
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/perfis/criar-por-template")]
    public async Task<ActionResult<ApiResponse<string>>> CriarPerfis(long tenantId, [FromBody] CriarPerfisTenantPorTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CriarPerfisTenantPorTemplateAsync(request with { TenantId = tenantId }, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<string>.Fail(result.Error ?? "Perfis inválidos.")) : Ok(ApiResponse<string>.Ok("Perfis criados."));
    }
}
