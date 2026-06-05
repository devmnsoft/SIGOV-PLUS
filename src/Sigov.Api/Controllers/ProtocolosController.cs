using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Processos;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/protocolos")]
[RequireModule("processos")]
[Produces("application/json")]
public sealed class ProtocolosController : ProcessosControllerBase
{
    private readonly IProtocoloAtendimentoService _service;
    public ProtocolosController(IProtocoloAtendimentoService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ProtocoloResumoResponse>>>> Listar([FromQuery] ProtocoloFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<ProtocoloDetalheResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CriarProtocoloRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] CriarProtocoloRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/converter-em-processo")] public async Task<ActionResult<ApiResponse<long>>> Converter(long id, [FromBody] ConverterProtocoloEmProcessoRequest request, CancellationToken ct) => FromResult(await _service.ConverterEmProcessoAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/encerrar")] public async Task<ActionResult<ApiResponse<object>>> Encerrar(long id, CancellationToken ct) => FromResult(await _service.EncerrarAsync(id, ct).ConfigureAwait(false));
}
