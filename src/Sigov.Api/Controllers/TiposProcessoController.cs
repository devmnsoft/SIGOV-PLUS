using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Processos;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/processos/tipos")]
[RequireModule("processos")]
[Produces("application/json")]
public sealed class TiposProcessoController : ProcessosControllerBase
{
    private readonly ITipoProcessoService _service;
    public TiposProcessoController(ITipoProcessoService service) => _service = service;
    [HttpGet] [ProducesResponseType(typeof(ApiResponse<PagedResult<TipoProcessoResponse>>), StatusCodes.Status200OK)] public async Task<ActionResult<ApiResponse<PagedResult<TipoProcessoResponse>>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => FromResult(await _service.ListarAsync(page, pageSize, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<TipoProcessoResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CriarTipoProcessoRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] AtualizarTipoProcessoRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken ct) => FromResult(await _service.ExcluirAsync(id, ct).ConfigureAwait(false));
}
