using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Comercial;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class AgroPainelComercialController : ControllerBase
{
    private readonly IAgroPainelComercialService _service;
    public AgroPainelComercialController(IAgroPainelComercialService service) => _service = service;
    [Authorize, RequireModule("agro"), HttpGet("api/agro/comercial/painel")] public async Task<ActionResult<ApiResponse<AgroPainelComercialResponse>>> Painel(CancellationToken ct) => FromResult(await _service.ObterAsync(ct).ConfigureAwait(false));
    [Authorize, RequireModule("agro"), HttpPut("api/agro/comercial/painel/config")] public async Task<ActionResult<ApiResponse<AgroPainelComercialResponse>>> Config([FromBody] AgroPainelComercialConfigRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(request, ct).ConfigureAwait(false));
    [AllowAnonymous, HttpGet("api/publico/agro/{tenantSlug}/painel")] public async Task<ActionResult<ApiResponse<AgroPainelComercialResponse>>> Publico(string tenantSlug, CancellationToken ct) => FromResult(await _service.ObterPublicoAsync(tenantSlug, ct).ConfigureAwait(false));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) { if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value)); if (result.Error == "403") return Forbid(); if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error)); return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida.")); }
}
