using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Processos;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/processos")]
[RequireModule("processos")]
[Produces("application/json")]
public sealed class ProcessosDigitaisController : ProcessosControllerBase
{
    private readonly IProcessoDigitalService _service;
    public ProcessosDigitaisController(IProcessoDigitalService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ProcessoResumoResponse>>>> Listar([FromQuery] ProcessoFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<ProcessoDetalheResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CriarProcessoRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] AtualizarProcessoRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken ct) => FromResult(await _service.ExcluirAsync(id, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/movimentar")] public async Task<ActionResult<ApiResponse<long>>> Movimentar(long id, [FromBody] MovimentarProcessoRequest request, CancellationToken ct) => FromResult(await _service.MovimentarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/pareceres")] public async Task<ActionResult<ApiResponse<long>>> Parecer(long id, [FromBody] EmitirParecerRequest request, CancellationToken ct) => FromResult(await _service.EmitirParecerAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/encerrar")] public async Task<ActionResult<ApiResponse<object>>> Encerrar(long id, CancellationToken ct) => FromResult(await _service.EncerrarAsync(id, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id, [FromBody] CancelarProcessoRequest request, CancellationToken ct) => FromResult(await _service.CancelarAsync(id, request, ct).ConfigureAwait(false));
}
