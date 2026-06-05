using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Common;
using Sigov.Application.Rh;
using Sigov.Application.Rh.Dto;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/rh")]
public sealed class RhTypedController : ProcessosControllerBase
{
    private readonly IRhTypedService _service;

    public RhTypedController(IRhTypedService service) => _service = service;

    [HttpGet("servidores-tipado")]
    public async Task<ActionResult<ApiResponse<PagedResult<ServidorResponse>>>> ListarServidores([FromQuery] ServidorFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarServidoresAsync(filtro, ct).ConfigureAwait(false));

    [HttpGet("servidores-tipado/{id:long}")]
    public async Task<ActionResult<ApiResponse<ServidorResponse>>> ObterServidor(long id, CancellationToken ct) =>
        FromResult(await _service.ObterServidorAsync(id, ct).ConfigureAwait(false));

    [HttpPost("servidores-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarServidor([FromBody] ServidorCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarServidorAsync(request, ct).ConfigureAwait(false));

    [HttpPut("servidores-tipado/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarServidor(long id, [FromBody] ServidorUpdateRequest request, CancellationToken ct) =>
        FromResult(await _service.AtualizarServidorAsync(id, request, ct).ConfigureAwait(false));

    [HttpDelete("servidores-tipado/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> ExcluirServidor(long id, CancellationToken ct) =>
        FromResult(await _service.ExcluirServidorAsync(id, ct).ConfigureAwait(false));

    [HttpGet("cargos-tipado")]
    public async Task<ActionResult<ApiResponse<PagedResult<CargoResponse>>>> ListarCargos([FromQuery] CargoFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarCargosAsync(filtro, ct).ConfigureAwait(false));

    [HttpPost("cargos-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarCargo([FromBody] CargoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarCargoAsync(request, ct).ConfigureAwait(false));

    [HttpGet("lotacoes-tipado")]
    public async Task<ActionResult<ApiResponse<PagedResult<LotacaoResponse>>>> ListarLotacoes([FromQuery] LotacaoFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarLotacoesAsync(filtro, ct).ConfigureAwait(false));

    [HttpPost("lotacoes-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarLotacao([FromBody] LotacaoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarLotacaoAsync(request, ct).ConfigureAwait(false));

    [HttpGet("vinculos-tipado")]
    public async Task<ActionResult<ApiResponse<PagedResult<VinculoResponse>>>> ListarVinculos([FromQuery] VinculoFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarVinculosAsync(filtro, ct).ConfigureAwait(false));

    [HttpPost("vinculos-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarVinculo([FromBody] VinculoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarVinculoAsync(request, ct).ConfigureAwait(false));

    [HttpGet("folhas-tipado")]
    public async Task<ActionResult<ApiResponse<PagedResult<FolhaResponse>>>> ListarFolhas([FromQuery] FolhaFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarFolhasAsync(filtro, ct).ConfigureAwait(false));

    [HttpPost("folhas-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarFolha([FromBody] FolhaCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarFolhaAsync(request, ct).ConfigureAwait(false));

    [HttpPost("folhas-tipado/{id:long}/fechar")]
    public async Task<ActionResult<ApiResponse<object>>> FecharFolha(long id, CancellationToken ct) =>
        FromResult(await _service.FecharFolhaAsync(id, ct).ConfigureAwait(false));

    [HttpPost("folhas-tipado/integrar-financeiro")]
    public async Task<ActionResult<ApiResponse<long>>> IntegrarFinanceiro([FromBody] RhFinanceiroIntegracaoRequest request, CancellationToken ct) =>
        FromResult(await _service.IntegrarFinanceiroAsync(request, ct).ConfigureAwait(false));

    [HttpPost("folhas-tipado/eventos")]
    public async Task<ActionResult<ApiResponse<long>>> CriarEventoFolha([FromBody] FolhaEventoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarEventoFolhaAsync(request, ct).ConfigureAwait(false));

    [HttpPost("folhas-tipado/{folhaId:long}/lancamentos")]
    public async Task<ActionResult<ApiResponse<long>>> CriarLancamentoFolha(long folhaId, [FromBody] FolhaLancamentoCreateRequest request, CancellationToken ct)
    {
        var payload = request with { FolhaId = folhaId };
        return FromResult(await _service.CriarLancamentoFolhaAsync(payload, ct).ConfigureAwait(false));
    }

    [HttpPost("pontos-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> RegistrarPonto([FromBody] PontoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.RegistrarPontoAsync(request, ct).ConfigureAwait(false));

    [HttpPost("ferias-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> ProgramarFerias([FromBody] FeriasCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.ProgramarFeriasAsync(request, ct).ConfigureAwait(false));

    [HttpPost("afastamentos-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> RegistrarAfastamento([FromBody] AfastamentoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.RegistrarAfastamentoAsync(request, ct).ConfigureAwait(false));

    [HttpPost("saude-ocupacional-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> RegistrarSaude([FromBody] SaudeOcupacionalCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.RegistrarSaudeOcupacionalAsync(request, ct).ConfigureAwait(false));

    [HttpPost("esocial-tipado")]
    public async Task<ActionResult<ApiResponse<long>>> CriarEsocial([FromBody] EsocialEventoCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarEventoEsocialAsync(request, ct).ConfigureAwait(false));

    [HttpGet("portal-tipado/servidores/{servidorId:long}")]
    public async Task<ActionResult<ApiResponse<PortalServidorResponse>>> PortalServidor(long servidorId, CancellationToken ct) =>
        FromResult(await _service.ObterPortalServidorAsync(servidorId, ct).ConfigureAwait(false));
}
