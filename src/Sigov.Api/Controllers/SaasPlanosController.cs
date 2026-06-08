using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Comercial;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasPlanosController : ControllerBase
{
    private readonly ISaasPlanoService _service;
    private readonly ICurrentUser _currentUser;
    public SaasPlanosController(ISaasPlanoService service, ICurrentUser currentUser) { _service = service; _currentUser = currentUser; }

    [HttpGet("api/publico/planos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>>> Publicos(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Ok(await _service.ListPublicAsync(cancellationToken).ConfigureAwait(false)));

    [HttpGet("api/publico/planos/{codigo}")]
    public async Task<ActionResult<ApiResponse<SaasPlanoDetalheResponse>>> Detalhe(string codigo, CancellationToken cancellationToken)
    {
        var plano = await _service.GetByCodigoAsync(codigo, cancellationToken).ConfigureAwait(false);
        return plano is null ? NotFound(ApiResponse<SaasPlanoDetalheResponse>.Fail("Plano não encontrado.")) : Ok(ApiResponse<SaasPlanoDetalheResponse>.Ok(plano));
    }

    [HttpGet("api/saas/planos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>>> Admin([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => Ok(ApiResponse<IReadOnlyCollection<SaasPlanoResponse>>.Ok(await _service.ListAdminAsync(page, pageSize, cancellationToken).ConfigureAwait(false)));

    [HttpPost("api/saas/planos")]
    public async Task<ActionResult<ApiResponse<SaasPlanoResponse>>> Criar([FromBody] SaasPlanoCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<SaasPlanoResponse>.Fail(result.Error ?? "Plano inválido.")) : Ok(ApiResponse<SaasPlanoResponse>.Ok(result.Value!));
    }

    [HttpPut("api/saas/planos/{id:long}")]
    public async Task<ActionResult<ApiResponse<SaasPlanoResponse>>> Atualizar(long id, [FromBody] SaasPlanoUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<SaasPlanoResponse>.Fail(result.Error ?? "Plano inválido.")) : Ok(ApiResponse<SaasPlanoResponse>.Ok(result.Value!));
    }
}
