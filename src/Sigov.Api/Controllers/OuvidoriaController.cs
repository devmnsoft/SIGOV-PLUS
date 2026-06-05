using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Processos;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ouvidoria")]
[RequireModule("processos")]
[Produces("application/json")]
public sealed class OuvidoriaController : ProcessosControllerBase
{
    private readonly IOuvidoriaService _service;
    public OuvidoriaController(IOuvidoriaService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<OuvidoriaResumoResponse>>>> Listar([FromQuery] OuvidoriaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<OuvidoriaDetalheResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CriarOuvidoriaRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/responder")] public async Task<ActionResult<ApiResponse<object>>> Responder(long id, [FromBody] ResponderOuvidoriaRequest request, CancellationToken ct) => FromResult(await _service.ResponderAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/converter-em-processo")] public async Task<ActionResult<ApiResponse<long>>> Converter(long id, [FromBody] ConverterProtocoloEmProcessoRequest request, CancellationToken ct) => FromResult(await _service.ConverterEmProcessoAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/arquivar")] public async Task<ActionResult<ApiResponse<object>>> Arquivar(long id, CancellationToken ct) => FromResult(await _service.ArquivarAsync(id, ct).ConfigureAwait(false));
}
