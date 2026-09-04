using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Bloco6;

namespace Sigov.Api.Controllers;

[ApiController]
public abstract class Bloco6ControllerBase : ControllerBase
{
    protected Bloco6Context Contexto()
    {
        var tenant = User.FindFirst("tenant_id")?.Value;
        var user = User.FindFirst("sub")?.Value ?? User.Identity?.Name ?? "system";
        if (!Guid.TryParse(tenant, out var tenantId))
            throw new UnauthorizedAccessException("Tenant inválido.");

        return new(tenantId, null, null, user, HttpContext.TraceIdentifier);
    }

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T value, string message = "Operação realizada.") =>
        Ok(ApiResponse<T>.Ok(value, message, HttpContext.TraceIdentifier));
}

[Authorize]
[RequireModule("compras")]
[Route("api/bloco6/compras")]
public sealed class ComprasBloco6Controller : Bloco6ControllerBase
{
    private readonly IComprasService _service;
    private readonly IComprasDashboardService _dashboard;

    public ComprasBloco6Controller(IComprasService service, IComprasDashboardService dashboard)
    {
        _service = service;
        _dashboard = dashboard;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<Bloco6DashboardDto>>> Dashboard(CancellationToken ct) =>
        OkResponse(await _dashboard.ObterAsync(Contexto(), ct));

    [HttpPost("solicitacoes")]
    public async Task<ActionResult<ApiResponse<object>>> CriarSolicitacao([FromBody] ComprasCriarSolicitacaoRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.CriarSolicitacaoAsync(Contexto(), request, ct) });

    [HttpPost("ordens-compra")]
    public async Task<ActionResult<ApiResponse<object>>> GerarOrdem([FromBody] ComprasGerarOrdemCompraRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.GerarOrdemAsync(Contexto(), request, ct) });

    [HttpGet("fornecedores"), HttpGet("solicitacoes"), HttpGet("cotacoes"), HttpGet("processos")]
    [HttpGet("ordens-compra"), HttpGet("relatorios/resumo"), HttpGet("relatorios/processos"), HttpGet("relatorios/fornecedores")]
    public ActionResult<ApiResponse<object>> Consultar() => OkResponse<object>(new { items = Array.Empty<object>() });

    [HttpPost("solicitacoes/{id:guid}/autorizar"), HttpPost("solicitacoes/{id:guid}/reprovar"), HttpPost("solicitacoes/{id:guid}/cancelar")]
    [HttpPost("cotacoes/{id:guid}/finalizar"), HttpPost("processos/{id:guid}/julgar"), HttpPost("processos/{id:guid}/homologar")]
    [HttpPost("processos/{id:guid}/cancelar"), HttpPost("ordens-compra/{id:guid}/integrar-financeiro")]
    public ActionResult<ApiResponse<object>> Acao(Guid id, [FromBody] object? request) =>
        OkResponse<object>(new { id, request, correlationId = HttpContext.TraceIdentifier });
}

[Authorize]
[RequireModule("contratos")]
[Route("api/bloco6/contratos")]
public sealed class ContratosBloco6Controller : Bloco6ControllerBase
{
    private readonly IContratosService _service;
    private readonly IContratosDashboardService _dashboard;

    public ContratosBloco6Controller(IContratosService service, IContratosDashboardService dashboard)
    {
        _service = service;
        _dashboard = dashboard;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<Bloco6DashboardDto>>> Dashboard(CancellationToken ct) =>
        OkResponse(await _dashboard.ObterAsync(Contexto(), ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Criar([FromBody] ContratoCriarRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.CriarAsync(Contexto(), request, ct) });

    [HttpPost("{id:guid}/medicoes")]
    public async Task<ActionResult<ApiResponse<object>>> Medir(Guid id, [FromBody] ContratoCriarMedicaoRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.MedirAsync(Contexto(), id, request, ct) });

    [HttpGet, HttpGet("alertas"), HttpGet("relatorios/resumo"), HttpGet("relatorios/vencimentos")]
    [HttpGet("relatorios/saldos"), HttpGet("{id:guid}/aditivos"), HttpGet("{id:guid}/apostilamentos"), HttpGet("{id:guid}/medicoes")]
    public ActionResult<ApiResponse<object>> Consultar() => OkResponse<object>(new { items = Array.Empty<object>() });

    [HttpPost("{id:guid}/ativar"), HttpPost("{id:guid}/suspender"), HttpPost("{id:guid}/encerrar"), HttpPost("{id:guid}/rescindir")]
    [HttpPost("{id:guid}/cancelar"), HttpPost("medicoes/{id:guid}/aprovar"), HttpPost("medicoes/{id:guid}/cancelar")]
    [HttpPost("medicoes/{id:guid}/integrar-financeiro")]
    public ActionResult<ApiResponse<object>> Acao(Guid id, [FromBody] object? request) => OkResponse<object>(new { id, request });
}

[Authorize]
[RequireModule("almoxarifado")]
[Route("api/bloco6/almoxarifado")]
public sealed class AlmoxarifadoBloco6Controller : Bloco6ControllerBase
{
    private readonly IAlmoxarifadoService _service;

    public AlmoxarifadoBloco6Controller(IAlmoxarifadoService service) => _service = service;

    [HttpPost("movimentos"), HttpPost("movimentos/entrada"), HttpPost("movimentos/saida"), HttpPost("movimentos/transferencia")]
    public async Task<ActionResult<ApiResponse<object>>> Movimentar([FromBody] AlmoxarifadoCriarMovimentoRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.MovimentarAsync(Contexto(), request, ct) });

    [HttpGet("dashboard"), HttpGet, HttpGet("itens"), HttpGet("estoque")]
    [HttpGet("movimentos"), HttpGet("inventarios"), HttpGet("relatorios/resumo")]
    public ActionResult<ApiResponse<object>> Consultar() => OkResponse<object>(new { items = Array.Empty<object>() });
}

[Authorize]
[RequireModule("patrimonio")]
[Route("api/bloco6/patrimonio")]
public sealed class PatrimonioBloco6Controller : Bloco6ControllerBase
{
    private readonly IPatrimonioService _service;

    public PatrimonioBloco6Controller(IPatrimonioService service) => _service = service;

    [HttpPost("bens")]
    public async Task<ActionResult<ApiResponse<object>>> Criar([FromBody] PatrimonioCriarBemRequest request, CancellationToken ct) =>
        OkResponse<object>(new { id = await _service.CriarBemAsync(Contexto(), request, ct) });

    [HttpGet("dashboard"), HttpGet("bens"), HttpGet("inventarios"), HttpGet("relatorios/resumo")]
    public ActionResult<ApiResponse<object>> Consultar() => OkResponse<object>(new { items = Array.Empty<object>() });

    [HttpPost("bens/{id:guid}/transferir"), HttpPost("bens/{id:guid}/baixar"), HttpPost("bens/{id:guid}/manutencao")]
    public ActionResult<ApiResponse<object>> Acao(Guid id, [FromBody] object request) => OkResponse<object>(new { id, request });
}
