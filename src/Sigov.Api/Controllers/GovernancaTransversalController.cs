using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Governanca;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class GovernancaTransversalController : ControllerBase
{
    private readonly ITransversalGovernancaService _service;
    public GovernancaTransversalController(ITransversalGovernancaService service) => _service = service;

    [HttpGet("api/pendencias")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PendenciaOperacionalDto>>>> Pendencias(
        [FromQuery] string? modulo, [FromQuery] string? gravidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyCollection<PendenciaOperacionalDto>>.Ok(await _service.ListarPendenciasAsync(modulo, gravidade, pagina, tamanho, ct).ConfigureAwait(false)));

    [HttpGet("api/alertas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AlertaOperacionalDto>>>> Alertas(
        [FromQuery] string? tipo, [FromQuery] string? severidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyCollection<AlertaOperacionalDto>>.Ok(await _service.ListarAlertasAsync(tipo, severidade, pagina, tamanho, ct).ConfigureAwait(false)));

    [HttpPost("api/alertas/{id:long}/resolver")]
    public async Task<ActionResult<ApiResponse<object>>> ResolverAlerta(long id, ResolverAlertaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Justificativa)) return BadRequest(ApiResponse<object>.Fail("Justificativa obrigatória."));
        return await _service.ResolverAlertaAsync(id, request.Justificativa, ct).ConfigureAwait(false)
            ? Ok(ApiResponse<object>.Ok(new { id, status = "RESOLVIDO" }))
            : NotFound(ApiResponse<object>.Fail("Alerta não encontrado."));
    }

    [HttpGet("api/qualidade-dados")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<QualidadeDadosDto>>>> Qualidade(
        [FromQuery] string? modulo, [FromQuery] string? severidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyCollection<QualidadeDadosDto>>.Ok(await _service.ListarQualidadeAsync(modulo, severidade, pagina, tamanho, ct).ConfigureAwait(false)));

    [HttpGet("api/governanca-transversal/integracoes-internas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<IntegracaoInternaDto>>>> Integracoes(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyCollection<IntegracaoInternaDto>>.Ok(await _service.ListarIntegracoesAsync(ct).ConfigureAwait(false)));

    [HttpGet("api/modulos/status-funcional")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ModuloStatusFuncionalDto>>>> StatusFuncional(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyCollection<ModuloStatusFuncionalDto>>.Ok(await _service.ListarStatusFuncionalAsync(ct).ConfigureAwait(false)));
}

public sealed record ResolverAlertaRequest(string Justificativa);
