using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;

namespace Sigov.Api.Controllers.Commercial;

[ApiController]
[Authorize]
public abstract class CommercialControllerBase(ICommercialApplicationService application) : ControllerBase
{
    protected ICommercialApplicationService Application { get; } = application;

    protected CommercialExecutionContext Context()
    {
        if (!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value ?? User.FindFirst("tenant_id")?.Value, out var tenantId) || tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("Tenant Enterprise não resolvido; o mapeamento explícito é obrigatório.");
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("Usuário não resolvido.");
        return new(tenantId, userId, User.HasClaim("permission", "comercial.clientes.dados_pessoais.visualizar"), HttpContext.TraceIdentifier);
    }
}

[Route("api/comercial/dashboard")]
public sealed class CommercialDashboardController(ICommercialApplicationService application) : CommercialControllerBase(application)
{
    [HttpGet]
    [Authorize(Policy = "comercial.dashboard.visualizar")]
    public async Task<ActionResult<ApiResponse<ComercialDashboardDto>>> Get(DateOnly? inicio, DateOnly? fim, CancellationToken ct)
    {
        var end = fim ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var data = await Application.ObterDashboardAsync(Context(), inicio ?? end.AddDays(-30), end, ct);
        return Ok(ApiResponse<ComercialDashboardDto>.Ok(data, correlationId: HttpContext.TraceIdentifier));
    }
}

[Route("api/comercial/clientes")]
public sealed class CommercialClientsController(ICommercialApplicationService application) : CommercialControllerBase(application)
{
    [HttpGet, Authorize(Policy = "comercial.clientes.visualizar")]
    public async Task<IActionResult> List([FromQuery] ClienteFiltro filtro, CancellationToken ct) => Ok(await Application.ListarClientesAsync(Context(), filtro, ct));
    [HttpGet("{id:guid}"), Authorize(Policy = "comercial.clientes.visualizar")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var value = await Application.ObterClienteAsync(Context(), id, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost, Authorize(Policy = "comercial.clientes.criar")]
    public async Task<IActionResult> Create(CriarClienteRequest request, CancellationToken ct) { var id = await Application.CriarClienteAsync(Context(), request, ct); return CreatedAtAction(nameof(Get), new { id }, id); }
}

[Route("api/comercial/leads")]
public sealed class CommercialLeadsController(ICommercialApplicationService application) : CommercialControllerBase(application)
{
    [HttpGet, Authorize(Policy = "comercial.leads.visualizar")]
    public async Task<IActionResult> List(int pagina = 1, int tamanho = 20, string? busca = null, CancellationToken ct = default) => Ok(await Application.ListarLeadsAsync(Context(), pagina, tamanho, busca, ct));
    [HttpPost, Authorize(Policy = "comercial.leads.criar")]
    public async Task<IActionResult> Create(CriarLeadRequest request, CancellationToken ct) => Ok(await Application.CriarLeadAsync(Context(), request, ct));
    [HttpPost("{id:guid}/converter"), Authorize(Policy = "comercial.leads.converter")]
    public async Task<IActionResult> Convert(Guid id, ConverterLeadRequest request, CancellationToken ct) => Ok(await Application.ConverterLeadAsync(Context(), id, request, ct));
}

[Route("api/comercial/oportunidades")]
public sealed class CommercialOpportunitiesController(ICommercialApplicationService application) : CommercialControllerBase(application)
{
    [HttpGet, Authorize(Policy = "comercial.oportunidades.visualizar")]
    public async Task<IActionResult> List(int pagina = 1, int tamanho = 100, string? fase = null, string? busca = null, CancellationToken ct = default) => Ok(await Application.ListarOportunidadesAsync(Context(), pagina, tamanho, fase, busca, ct));
    [HttpPost("{id:guid}/mover-fase"), Authorize(Policy = "comercial.oportunidades.editar")]
    public async Task<IActionResult> Move(Guid id, MoverOportunidadeRequest request, CancellationToken ct) { await Application.MoverFaseAsync(Context(), id, request, ct); return NoContent(); }
}

[Route("api/comercial/propostas")]
public sealed class CommercialProposalsController(ICommercialApplicationService application) : CommercialControllerBase(application)
{
    [HttpGet, Authorize(Policy = "comercial.propostas.visualizar")]
    public async Task<IActionResult> List(int pagina = 1, int tamanho = 20, CancellationToken ct = default) => Ok(await Application.ListarPropostasAsync(Context(), pagina, tamanho, ct));
    [HttpGet("{id:guid}"), Authorize(Policy = "comercial.propostas.visualizar")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var value = await Application.ObterPropostaAsync(Context(), id, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost, Authorize(Policy = "comercial.propostas.criar")]
    public async Task<IActionResult> Create(CriarPropostaRequest request, CancellationToken ct) { var id = await Application.CriarPropostaAsync(Context(), request, ct); return CreatedAtAction(nameof(Get), new { id }, id); }
    [HttpPost("{id:guid}/emitir"), Authorize(Policy = "comercial.propostas.emitir")]
    public async Task<IActionResult> Issue(Guid id, EmitirPropostaRequest request, CancellationToken ct) { await Application.EmitirAsync(Context(), id, request.Version, ct); return NoContent(); }
    [HttpPost("{id:guid}/aprovar"), Authorize(Policy = "comercial.propostas.aprovar")]
    public async Task<IActionResult> Approve(Guid id, DecidirPropostaRequest request, CancellationToken ct) { await Application.AprovarAsync(Context(), id, request.Version, ct); return NoContent(); }
    [HttpPost("{id:guid}/gerar-pedido"), Authorize(Policy = "comercial.pedidos.criar")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct) => Ok(await Application.GerarPedidoAsync(Context(), id, Request.Headers["Idempotency-Key"].ToString(), ct));
}

[Route("api/comercial/pedidos")]
public sealed class CommercialOrdersController(ICommercialApplicationService application, Sigov.Application.OrdemServico.IOrdemServicoApplicationService ordensServico) : CommercialControllerBase(application)
{
    [HttpGet, Authorize(Policy = "comercial.pedidos.visualizar")]
    public async Task<IActionResult> List(int pagina = 1, int tamanho = 20, CancellationToken ct = default) => Ok(await Application.ListarPedidosAsync(Context(), pagina, tamanho, ct));
    [HttpPost("{id:guid}/gerar-os"), Authorize(Policy = "os.ordens.criar")]
    public async Task<IActionResult> ServiceOrder(Guid id, CancellationToken ct) { var c=Context(); return Ok(await ordensServico.GerarDoPedidoAsync(new(c.TenantId,c.UsuarioId,c.CorrelationId),id,Request.Headers["Idempotency-Key"].ToString(),ct)); }
    [HttpPost("{id:guid}/confirmar"), Authorize(Policy = "comercial.pedidos.confirmar")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmarPedidoRequest request, CancellationToken ct) { await Application.ConfirmarPedidoAsync(Context(), id, request.Version, Request.Headers["Idempotency-Key"].ToString(), ct); return NoContent(); }
}
