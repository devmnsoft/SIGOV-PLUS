using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Governanca;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
public sealed class GovernancaTransversalController : ControllerBase
{
    private readonly ITransversalGovernancaService _service;
    private readonly ICurrentTenant _tenant;
    public GovernancaTransversalController(ITransversalGovernancaService service, ICurrentTenant tenant) => (_service, _tenant) = (service, tenant);

    [HttpGet("api/pendencias")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PendenciaOperacionalDto>>>> Pendencias(
        [FromQuery] string? modulo, [FromQuery] string? gravidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        { if (!HasContext()) return ContextRequired<IReadOnlyCollection<PendenciaOperacionalDto>>(); return Ok(ApiResponse<IReadOnlyCollection<PendenciaOperacionalDto>>.Ok(await _service.ListarPendenciasAsync(modulo, gravidade, pagina, tamanho, ct).ConfigureAwait(false))); }

    [HttpGet("api/alertas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AlertaOperacionalDto>>>> Alertas(
        [FromQuery] string? tipo, [FromQuery] string? severidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        { if (!HasContext()) return ContextRequired<IReadOnlyCollection<AlertaOperacionalDto>>(); return Ok(ApiResponse<IReadOnlyCollection<AlertaOperacionalDto>>.Ok(await _service.ListarAlertasAsync(tipo, severidade, pagina, tamanho, ct).ConfigureAwait(false))); }

    [HttpPost("api/alertas/{id:long}/resolver")]
    public async Task<ActionResult<ApiResponse<object>>> ResolverAlerta(long id, ResolverAlertaRequest request, CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired<object>();
        if (string.IsNullOrWhiteSpace(request.Justificativa)) return BadRequest(ApiResponse<object>.Fail("Justificativa obrigatória."));
        return await _service.ResolverAlertaAsync(id, request.Justificativa, ct).ConfigureAwait(false)
            ? Ok(ApiResponse<object>.Ok(new { id, status = "RESOLVIDO" }))
            : NotFound(ApiResponse<object>.Fail("Alerta não encontrado."));
    }

    [HttpGet("api/qualidade-dados")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<QualidadeDadosDto>>>> Qualidade(
        [FromQuery] string? modulo, [FromQuery] string? severidade, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 50, CancellationToken ct = default)
        { if (!HasContext()) return ContextRequired<IReadOnlyCollection<QualidadeDadosDto>>(); return Ok(ApiResponse<IReadOnlyCollection<QualidadeDadosDto>>.Ok(await _service.ListarQualidadeAsync(modulo, severidade, pagina, tamanho, ct).ConfigureAwait(false))); }

    [HttpGet("api/governanca/ocorrencias/{tipo}/{id:long}")]
    public async Task<ActionResult<ApiResponse<GovernancaOcorrenciaDto>>> Ocorrencia(string tipo, long id, CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired<GovernancaOcorrenciaDto>();
        var item = await _service.ObterOcorrenciaAsync(tipo, id, ct).ConfigureAwait(false);
        return item is null ? NotFound(ApiResponse<GovernancaOcorrenciaDto>.Fail("Ocorrência não encontrada no contexto selecionado.")) : Ok(ApiResponse<GovernancaOcorrenciaDto>.Ok(item));
    }

    [HttpPost("api/governanca/ocorrencias/{tipo}/{id:long}/atribuir")]
    public async Task<ActionResult<ApiResponse<GovernancaComandoResultado>>> Atribuir(string tipo, long id, GovernancaAtribuirRequest request, CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired<GovernancaComandoResultado>();
        var result = await _service.AtribuirAsync(tipo, id, request.ResponsavelUsuarioId, request.Versao, request.Justificativa, ct).ConfigureAwait(false);
        return result.Sucesso ? Ok(ApiResponse<GovernancaComandoResultado>.Ok(result)) : Conflict(ApiResponse<GovernancaComandoResultado>.Fail(result.Mensagem));
    }

    [HttpGet("api/governanca/responsaveis-elegiveis")]
    public async Task<ActionResult<ApiResponse<ResponsaveisElegiveisPaginaDto>>> ResponsaveisElegiveis(
        [FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20, CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired<ResponsaveisElegiveisPaginaDto>();
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 50);
        var encontrados = await _service.BuscarResponsaveisAsync(busca, pagina, tamanho + 1, ct).ConfigureAwait(false);
        var resposta = new ResponsaveisElegiveisPaginaDto(encontrados.Take(tamanho).ToArray(), pagina, tamanho,
            encontrados.Count > tamanho);
        return Ok(ApiResponse<ResponsaveisElegiveisPaginaDto>.Ok(resposta));
    }

    [HttpPost("api/governanca/ocorrencias/qualidade/{id:long}/revalidar")]
    public async Task<ActionResult<ApiResponse<GovernancaComandoResultado>>> Revalidar(long id, GovernancaRevalidarRequest request, CancellationToken ct)
    {
        if (!HasContext()) return ContextRequired<GovernancaComandoResultado>();
        var result = await _service.RevalidarQualidadeAsync(id, request.Versao, ct).ConfigureAwait(false);
        return result.Sucesso ? Ok(ApiResponse<GovernancaComandoResultado>.Ok(result)) : Conflict(ApiResponse<GovernancaComandoResultado>.Fail(result.Mensagem));
    }

    [HttpGet("api/governanca-transversal/integracoes-internas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<IntegracaoInternaDto>>>> Integracoes(CancellationToken ct)
        { if (!HasContext()) return ContextRequired<IReadOnlyCollection<IntegracaoInternaDto>>(); return Ok(ApiResponse<IReadOnlyCollection<IntegracaoInternaDto>>.Ok(await _service.ListarIntegracoesAsync(ct).ConfigureAwait(false))); }

    [HttpGet("api/modulos/status-funcional")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ModuloStatusFuncionalDto>>>> StatusFuncional(CancellationToken ct)
        { if (!HasContext()) return ContextRequired<IReadOnlyCollection<ModuloStatusFuncionalDto>>(); return Ok(ApiResponse<IReadOnlyCollection<ModuloStatusFuncionalDto>>.Ok(await _service.ListarStatusFuncionalAsync(ct).ConfigureAwait(false))); }

    private bool HasContext() => _tenant.TenantId is > 0;
    private ActionResult<ApiResponse<T>> ContextRequired<T>() => StatusCode(StatusCodes.Status428PreconditionRequired,
        ApiResponse<T>.Fail("CONTEXTO_OBRIGATORIO: selecione um contexto institucional autorizado."));
}

public sealed record ResolverAlertaRequest(string Justificativa);
public sealed record GovernancaAtribuirRequest(long ResponsavelUsuarioId, long Versao, string Justificativa);
public sealed record GovernancaRevalidarRequest(long Versao);
