using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Enterprise;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class EnterpriseModulesController : ControllerBase
{
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IEnterpriseModuleService _service;
    private readonly ILogger<EnterpriseModulesController> _logger;

    public EnterpriseModulesController(IEnterpriseModuleService service, ILogger<EnterpriseModulesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("api/comercial/clientes")]
    [HttpGet("api/comercial/leads")]
    [HttpGet("api/comercial/oportunidades")]
    [HttpGet("api/comercial/propostas")]
    [HttpGet("api/comercial/pedidos")]
    [HttpGet("api/industrial/ativos")]
    [HttpGet("api/industrial/planos-manutencao")]
    [HttpGet("api/industrial/medidores")]
    [HttpGet("api/industrial/paradas")]
    [HttpGet("api/estoque/produtos")]
    [HttpGet("api/estoque/almoxarifados")]
    [HttpGet("api/compras/fornecedores")]
    [HttpGet("api/compras/pedidos")]
    [HttpGet("api/comercial/tabelas-preco")]
    [HttpGet("api/comercial/comissoes")]
    [HttpGet("api/comercio/clientes")]
    [HttpGet("api/comercio/produtos")]
    [HttpGet("api/comercio/orcamentos")]
    [HttpGet("api/comercio/pedidos")]
    [HttpGet("api/comercio/tabelas-preco")]
    public ActionResult<ApiResponse<IReadOnlyList<EnterpriseListItem>>> List()
    {
        try
        {
            var tenantId = ResolveTenantId();
            return Ok(ApiResponse<IReadOnlyList<EnterpriseListItem>>.Ok(_service.List(Area(), tenantId), correlationId: CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar módulo empresarial. CorrelationId={CorrelationId}", CorrelationId());
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<IReadOnlyList<EnterpriseListItem>>.Fail("Falha controlada ao listar módulo empresarial.", CorrelationId()));
        }
    }

    [HttpPost("api/comercial/clientes")]
    [HttpPut("api/comercial/clientes")]
    [HttpPost("api/comercial/leads")]
    [HttpPut("api/comercial/leads")]
    [HttpPost("api/comercial/oportunidades")]
    [HttpPut("api/comercial/oportunidades")]
    [HttpPost("api/comercial/propostas")]
    [HttpPut("api/comercial/propostas")]
    [HttpPost("api/comercial/pedidos")]
    [HttpPut("api/comercial/pedidos")]
    [HttpPost("api/industrial/ativos")]
    [HttpPut("api/industrial/ativos")]
    [HttpPost("api/industrial/planos-manutencao")]
    [HttpPut("api/industrial/planos-manutencao")]
    [HttpPost("api/industrial/medidores")]
    [HttpPost("api/industrial/paradas")]
    [HttpPost("api/estoque/produtos")]
    [HttpPut("api/estoque/produtos")]
    [HttpPost("api/estoque/almoxarifados")]
    [HttpPut("api/estoque/almoxarifados")]
    [HttpPost("api/estoque/requisicoes")]
    [HttpPost("api/compras/fornecedores")]
    [HttpPut("api/compras/fornecedores")]
    [HttpPost("api/compras/pedidos")]
    [HttpPut("api/compras/pedidos")]
    [HttpPost("api/comercial/tabelas-preco")]
    [HttpPut("api/comercial/tabelas-preco")]
    [HttpPost("api/comercial/comissoes")]
    [HttpPut("api/comercial/comissoes")]
    [HttpPost("api/comercio/clientes")]
    [HttpPut("api/comercio/clientes")]
    [HttpPost("api/comercio/produtos")]
    [HttpPut("api/comercio/produtos")]
    [HttpPost("api/comercio/orcamentos")]
    [HttpPut("api/comercio/orcamentos")]
    [HttpPost("api/comercio/pedidos")]
    [HttpPut("api/comercio/pedidos")]
    [HttpPost("api/comercio/tabelas-preco")]
    [HttpPut("api/comercio/tabelas-preco")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> Upsert([FromBody] EnterpriseMutationRequest request)
    {
        try
        {
            var tenantId = ResolveTenantId();
            var result = _service.Upsert(Area(), request, tenantId, CorrelationId());
            return result.Status == "FORBIDDEN"
                ? StatusCode(StatusCodes.Status403Forbidden, ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId()))
                : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar módulo empresarial. CorrelationId={CorrelationId}", CorrelationId());
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<EnterpriseActionResult>.Fail("Falha controlada ao salvar módulo empresarial.", CorrelationId()));
        }
    }

    [HttpPost("api/comercial/propostas/{id:guid}/aprovar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> ApproveProposal(Guid id) => Execute(id, _service.ApproveProposal);

    [HttpPost("api/comercial/propostas/{id:guid}/reprovar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> RejectProposal(Guid id) => Execute(id, _service.RejectProposal);

    [HttpPost("api/comercial/propostas/{id:guid}/gerar-pedido")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> GenerateOrder(Guid id) => Execute(id, _service.GenerateOrderFromProposal);

    [HttpPost("api/comercial/pedidos/{id:guid}/confirmar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> ConfirmOrder(Guid id) => Execute(id, _service.ConfirmCommercialOrder);

    [HttpPost("api/comercial/pedidos/{id:guid}/cancelar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> CancelOrder(Guid id) => Execute(id, _service.CancelCommercialOrder);

    [HttpPost("api/comercial/pedidos/{id:guid}/gerar-os")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> GenerateServiceOrder(Guid id) => Execute(id, _service.GenerateServiceOrderFromOrder);

    [HttpGet("api/os/ordens")]
    public ActionResult<ApiResponse<IReadOnlyList<EnterpriseListItem>>> ServiceOrders() => Ok(ApiResponse<IReadOnlyList<EnterpriseListItem>>.Ok(_service.List("os/ordens", ResolveTenantId()), correlationId: CorrelationId()));

    [HttpPost("api/os/ordens")]
    [HttpPut("api/os/ordens")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> UpsertServiceOrder([FromBody] EnterpriseMutationRequest request) => Upsert(request);

    [HttpGet("api/os/ordens/{id:guid}")]
    public ActionResult<ApiResponse<OrdemServicoDetail>> ServiceOrder(Guid id) => Ok(ApiResponse<OrdemServicoDetail>.Ok(_service.GetServiceOrder(id, ResolveTenantId()), correlationId: CorrelationId()));

    [HttpPost("api/os/ordens/{id:guid}/agendar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> Schedule(Guid id) => ServiceStatus(id, "AGENDADA");

    [HttpPost("api/os/ordens/{id:guid}/iniciar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> Start(Guid id) => ServiceStatus(id, "EM_EXECUCAO");

    [HttpPost("api/os/ordens/{id:guid}/pausar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> Pause(Guid id) => ServiceStatus(id, "AGUARDANDO_CLIENTE");

    [HttpPost("api/os/ordens/{id:guid}/concluir")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> Finish(Guid id) => ServiceStatus(id, "CONCLUIDA_EVENTO_FINANCEIRO_FUTURO");

    [HttpPost("api/os/ordens/{id:guid}/cancelar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> CancelServiceOrder(Guid id) => ServiceStatus(id, "CANCELADA");

    [HttpPost("api/os/ordens/{id:guid}/apontamentos")]
    [HttpPost("api/os/ordens/{id:guid}/checklist")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> AddServiceEntry(Guid id) => Ok(ApiResponse<EnterpriseActionResult>.Ok(_service.AddServiceOrderEntry(id, ResolveTenantId(), Area(), CorrelationId()), correlationId: CorrelationId()));

    [HttpPost("api/os/ordens/{id:guid}/consumir-peca")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> ConsumePiece(Guid id, [FromBody] EnterpriseMutationRequest request)
    {
        var result = _service.ConsumeStock(id, ResolveTenantId(), request.ProdutoId.GetValueOrDefault(Guid.Parse("11111111-1111-1111-1111-111111111111")), request.Quantidade.GetValueOrDefault(1), request.PermitirSaldoNegativo.GetValueOrDefault(), CorrelationId());
        return result.Status == "SALDO_INSUFICIENTE" ? Conflict(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    [HttpPost("api/industrial/planos-manutencao/{id:guid}/gerar-os")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> GeneratePreventive(Guid id) => Execute(id, _service.GeneratePreventiveServiceOrder);

    [HttpPost("api/industrial/medidores/{id:guid}/leituras")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> AddReading(Guid id, [FromBody] EnterpriseMutationRequest request) => Ok(ApiResponse<EnterpriseActionResult>.Ok(_service.AddMeterReading(id, ResolveTenantId(), request.Valor.GetValueOrDefault(), CorrelationId()), correlationId: CorrelationId()));

    [HttpGet("api/estoque/saldos")]
    public ActionResult<ApiResponse<IReadOnlyList<EstoqueSaldo>>> Stock() => Ok(ApiResponse<IReadOnlyList<EstoqueSaldo>>.Ok(_service.GetStock(ResolveTenantId()), correlationId: CorrelationId()));

    [HttpPost("api/estoque/movimentos/entrada")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> StockIn([FromBody] EnterpriseMutationRequest request) => StockMove(request, "ENTRADA");

    [HttpPost("api/estoque/movimentos/saida")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> StockOut([FromBody] EnterpriseMutationRequest request) => StockMove(request, "SAIDA");

    [HttpPost("api/estoque/movimentos/ajuste")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> StockAdjust([FromBody] EnterpriseMutationRequest request) => StockMove(request, "AJUSTE");

    [HttpPost("api/estoque/requisicoes/{id:guid}/aprovar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> ApproveRequisition(Guid id) => Execute(id, (itemId, tenantId, correlationId) => new EnterpriseActionResult(itemId, tenantId, "APROVADA", $"Requisição aprovada. CorrelationId={correlationId}"));

    [HttpPost("api/estoque/requisicoes/{id:guid}/baixar")]
    public ActionResult<ApiResponse<EnterpriseActionResult>> CloseRequisition(Guid id) => Execute(id, (itemId, tenantId, correlationId) => new EnterpriseActionResult(itemId, tenantId, "BAIXADA", $"Requisição baixada. CorrelationId={correlationId}"));


    [HttpGet("api/comercial/clientes/export-csv")]
    [HttpGet("api/comercial/propostas/export-csv")]
    [HttpGet("api/comercial/pedidos/export-csv")]
    [HttpGet("api/os/ordens/export-csv")]
    [HttpGet("api/estoque/produtos/export-csv")]
    [HttpGet("api/compras/fornecedores/export-csv")]
    [HttpGet("api/industrial/ativos/export-csv")]
    [HttpGet("api/comercio/clientes/export-csv")]
    [HttpGet("api/comercio/produtos/export-csv")]
    [HttpGet("api/comercio/orcamentos/export-csv")]
    [HttpGet("api/comercio/pedidos/export-csv")]
    public IActionResult ExportCsv()
    {
        var rows = _service.List(Area().Replace("/export-csv", string.Empty, StringComparison.OrdinalIgnoreCase), ResolveTenantId());
        var csv = "id;nome;status;documento_mascarado;email_mascarado;telefone_mascarado;updated_at\n" + string.Join("\n", rows.Select(r => $"{r.Id};{SanitizeCsv(r.Name)};{r.Status};{r.DocumentMasked};{r.EmailMasked};{r.PhoneMasked};{r.UpdatedAt:O}"));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"enterprise-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("api/enterprise/{module}/dashboard")]
    public ActionResult<ApiResponse<EnterpriseDashboard>> Dashboard(string module) => Ok(ApiResponse<EnterpriseDashboard>.Ok(_service.GetDashboard(module, ResolveTenantId()), correlationId: CorrelationId()));

    private ActionResult<ApiResponse<EnterpriseActionResult>> ServiceStatus(Guid id, string status) => Ok(ApiResponse<EnterpriseActionResult>.Ok(_service.ChangeServiceOrderStatus(id, ResolveTenantId(), status, CorrelationId()), correlationId: CorrelationId()));

    private ActionResult<ApiResponse<EnterpriseActionResult>> StockMove(EnterpriseMutationRequest request, string movement)
    {
        var result = _service.MoveStock(ResolveTenantId(), request.ProdutoId.GetValueOrDefault(Guid.Parse("11111111-1111-1111-1111-111111111111")), request.Quantidade.GetValueOrDefault(1), movement, request.PermitirSaldoNegativo.GetValueOrDefault(), CorrelationId());
        return result.Status == "SALDO_INSUFICIENTE" ? Conflict(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    private ActionResult<ApiResponse<EnterpriseActionResult>> Execute(Guid id, Func<Guid, Guid, string, EnterpriseActionResult> action)
    {
        try
        {
            return Ok(ApiResponse<EnterpriseActionResult>.Ok(action(id, ResolveTenantId(), CorrelationId()), correlationId: CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em ação empresarial. CorrelationId={CorrelationId}", CorrelationId());
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<EnterpriseActionResult>.Fail("Falha controlada na ação empresarial.", CorrelationId()));
        }
    }

    private Guid ResolveTenantId()
    {
        var value = Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(value, out var tenantId) ? tenantId : DemoTenantId;
    }

    private static string SanitizeCsv(string? value) => (value ?? string.Empty).Replace(";", ",", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal);

    private string Area() => Request.Path.Value?.Trim('/').Replace("api/", string.Empty, StringComparison.OrdinalIgnoreCase) ?? "enterprise";

    private string CorrelationId() => HttpContext.TraceIdentifier;
}
