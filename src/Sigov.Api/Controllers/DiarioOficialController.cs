using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Common;
using Sigov.Application.Processos;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/diario-oficial/publicacoes")]
[RequireModule("processos")]
[Produces("application/json")]
public sealed class DiarioOficialController : ProcessosControllerBase
{
    private readonly IDiarioOficialService _service;
    public DiarioOficialController(IDiarioOficialService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<DiarioPublicacaoResponse>>>> Listar([FromQuery] DiarioFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<DiarioPublicacaoResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CriarDiarioPublicacaoRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] CriarDiarioPublicacaoRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/publicar")] public async Task<ActionResult<ApiResponse<object>>> Publicar(long id, [FromBody] PublicarDiarioRequest request, CancellationToken ct) => FromResult(await _service.PublicarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/atos")] public async Task<ActionResult<ApiResponse<long>>> CriarAto(long id, [FromBody] CriarAtoOficialRequest request, CancellationToken ct) => FromResult(await _service.CriarAtoAsync(id, request, ct).ConfigureAwait(false));
    [HttpGet("{id:long}/atos")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AtoOficialResponse>>>> Atos(long id, CancellationToken ct) => FromResult(await _service.ListarAtosAsync(id, ct).ConfigureAwait(false));
}
