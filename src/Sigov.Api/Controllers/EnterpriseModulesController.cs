using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Api.Contracts;
using Sigov.Application.Enterprise;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
public sealed class EnterpriseModulesController : ControllerBase, IAsyncActionFilter
{
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IEnterpriseModuleService _service;
    private readonly IEnterpriseCrudService? _crud;
    private readonly ILogger<EnterpriseModulesController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public EnterpriseModulesController(IEnterpriseModuleService service, ILogger<EnterpriseModulesController> logger, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _service = service;
        _crud = service as IEnterpriseCrudService;
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var path = Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/enterprise", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/comercial", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/os", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/estoque", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/compras", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/industrial", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/industria", StringComparison.OrdinalIgnoreCase))
        {
            var action = ResolveEnterpriseAction(path);
            var area = Area().Replace("export-csv", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('/');
            var permission = PermissionFor(area, action);
            var denied = EnsureTenantAndPermission(permission);
            if (denied is not null) { context.Result = denied; return; }

            var tenantId = ResolveTenantId();
            var login = User.Identity?.Name ?? User.FindFirst("preferred_username")?.Value ?? User.FindFirst("sub")?.Value ?? "usuario.autenticado";
            EnterpriseExecutionContextAccessor.Current = new EnterpriseExecutionContext(tenantId, User.FindFirst("sub")?.Value ?? login, login, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), CorrelationId(), User.Claims.Select(c => c.Value).ToArray());
        }

        try { await next(); }
        finally { EnterpriseExecutionContextAccessor.Current = null; }
    }


    [HttpGet("api/enterprise/{area}/export-csv")]
    public async Task<IActionResult> EnterpriseExport(string area, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<string>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var csv = await _crud.ExportCsvAsync(NormalizeEnterpriseArea(area), ResolveTenantId(), cancellationToken);
        return File(csv, "text/csv", $"enterprise-{area}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("api/enterprise/{area}")]
    [HttpGet("api/industria/{area}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EnterpriseListItem>>>> EnterpriseList(string area, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (_crud is null) return NotFound(ApiResponse<IReadOnlyList<EnterpriseListItem>>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var prefix = Request.Path.Value?.StartsWith("/api/industria/", StringComparison.OrdinalIgnoreCase) == true ? "industria" : "enterprise";
        var rows = await _crud.ListAsync(NormalizeEnterpriseArea(area, prefix), ResolveTenantId(), page, pageSize, search, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnterpriseListItem>>.Ok(rows, correlationId: CorrelationId()));
    }

    [HttpGet("api/enterprise/{area}/{id:guid}")]
    [HttpGet("api/industria/{area}/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EnterpriseListItem>>> EnterpriseGet(string area, Guid id, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseListItem>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var prefix = Request.Path.Value?.StartsWith("/api/industria/", StringComparison.OrdinalIgnoreCase) == true ? "industria" : "enterprise";
        var row = await _crud.GetByIdAsync(NormalizeEnterpriseArea(area, prefix), id, ResolveTenantId(), cancellationToken);
        return row is null ? NotFound(ApiResponse<EnterpriseListItem>.Fail("Registro não encontrado no tenant.", CorrelationId())) : Ok(ApiResponse<EnterpriseListItem>.Ok(row, correlationId: CorrelationId()));
    }

    [HttpPost("api/enterprise/{area}")]
    [HttpPost("api/industria/{area}")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> EnterpriseCreate(string area, [FromBody] EnterpriseMutationRequest request, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var prefix = Request.Path.Value?.StartsWith("/api/industria/", StringComparison.OrdinalIgnoreCase) == true ? "industria" : "enterprise";
        var result = await _crud.CreateAsync(NormalizeEnterpriseArea(area, prefix), request, ResolveTenantId(), CorrelationId(), cancellationToken);
        return Created($"{Request.Path}/{result.Id}", ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    [HttpPut("api/enterprise/{area}/{id:guid}")]
    [HttpPut("api/industria/{area}/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> EnterpriseUpdate(string area, Guid id, [FromBody] EnterpriseMutationRequest request, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var prefix = Request.Path.Value?.StartsWith("/api/industria/", StringComparison.OrdinalIgnoreCase) == true ? "industria" : "enterprise";
        var result = await _crud.UpdateAsync(NormalizeEnterpriseArea(area, prefix), id, request, ResolveTenantId(), CorrelationId(), cancellationToken);
        return result.Status == "NOT_FOUND" ? NotFound(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    [HttpDelete("api/enterprise/{area}/{id:guid}")]
    [HttpDelete("api/industria/{area}/{id:guid}")]
    [HttpPost("api/enterprise/{area}/{id:guid}/inativar")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> EnterpriseDelete(string area, Guid id, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var prefix = Request.Path.Value?.StartsWith("/api/industria/", StringComparison.OrdinalIgnoreCase) == true ? "industria" : "enterprise";
        var result = await _crud.DeleteAsync(NormalizeEnterpriseArea(area, prefix), id, ResolveTenantId(), CorrelationId(), cancellationToken);
        return result.Status == "NOT_FOUND" ? NotFound(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    [HttpPost("api/enterprise/{area}/{id:guid}/restaurar")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> EnterpriseRestore(string area, Guid id, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var result = await _crud.RestoreAsync(NormalizeEnterpriseArea(area), id, ResolveTenantId(), CorrelationId(), cancellationToken);
        return result.Status == "NOT_FOUND" ? NotFound(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }



    [HttpGet("api/{segment}/{area}/export-csv")]
    public async Task<IActionResult> LegacyExport(string segment, string area, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<string>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var csv = await _crud.ExportCsvAsync($"{segment}/{area}", ResolveTenantId(), cancellationToken);
        return File(csv, "text/csv", $"enterprise-{segment}-{area}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPut("api/{segment}/{area}/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> LegacyUpdate(string segment, string area, Guid id, [FromBody] EnterpriseMutationRequest request, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var result = await _crud.UpdateAsync($"{segment}/{area}", id, request, ResolveTenantId(), CorrelationId(), cancellationToken);
        return result.Status == "NOT_FOUND" ? NotFound(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
    }

    [HttpDelete("api/{segment}/{area}/{id:guid}")]
    public async Task<ActionResult<ApiResponse<EnterpriseActionResult>>> LegacyDelete(string segment, string area, Guid id, CancellationToken cancellationToken)
    {
        if (_crud is null) return NotFound(ApiResponse<EnterpriseActionResult>.Fail("CRUD Enterprise indisponível.", CorrelationId()));
        var result = await _crud.DeleteAsync($"{segment}/{area}", id, ResolveTenantId(), CorrelationId(), cancellationToken);
        return result.Status == "NOT_FOUND" ? NotFound(ApiResponse<EnterpriseActionResult>.Fail(result.Message, CorrelationId())) : Ok(ApiResponse<EnterpriseActionResult>.Ok(result, correlationId: CorrelationId()));
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
        if (Guid.TryParse(value, out var tenantId)) return tenantId;

        var allowDemo = _configuration.GetValue<bool>("Enterprise:AllowDemoTenantFallback");
        if (!_environment.IsProduction() && allowDemo)
        {
            _logger.LogWarning("Enterprise usando tenant demo por fallback explícito. Environment={Environment}; CorrelationId={CorrelationId}", _environment.EnvironmentName, CorrelationId());
            return DemoTenantId;
        }

        throw new InvalidOperationException("TENANT_REQUIRED");
    }

    private IActionResult? EnsureTenantAndPermission(string permission)
    {
        if (!User.Identity?.IsAuthenticated ?? true) return Unauthorized(ApiResponse<string>.Fail("Autenticação obrigatória para API Enterprise.", CorrelationId()));
        try { _ = ResolveTenantId(); }
        catch (InvalidOperationException ex) when (ex.Message == "TENANT_REQUIRED")
        {
            return BadRequest(ApiResponse<string>.Fail("Tenant obrigatório. Informe X-Tenant-Id válido; fallback demo é proibido em produção.", CorrelationId()));
        }

        if (HasEnterprisePermission(permission)) return null;
        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail($"Permissão Enterprise ausente: {permission}.", CorrelationId()));
    }

    private static string ResolveEnterpriseAction(string path)
    {
        if (path.Contains("export-csv", StringComparison.OrdinalIgnoreCase)) return "CSV";
        foreach (var action in new[] { "aprovar", "reprovar", "confirmar", "cancelar", "iniciar", "concluir", "agendar", "pausar", "gerar-pedido", "gerar-os", "entrada", "saida", "ajuste" })
            if (path.EndsWith("/" + action, StringComparison.OrdinalIgnoreCase) || path.Contains("/" + action + "/", StringComparison.OrdinalIgnoreCase)) return action.ToUpperInvariant().Replace('-', '_');
        return "";
    }

    private bool HasEnterprisePermission(string permission)
    {
        if (User.IsInRole("ADMIN_GERAL") || User.HasClaim("role", "ADMIN_GERAL")) return true;
        if (User.IsInRole("ADMIN_TENANT") || User.HasClaim("role", "ADMIN_TENANT")) return true;
        return User.Claims.Any(c => (c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("permissions", StringComparison.OrdinalIgnoreCase) || c.Type.Equals("scope", StringComparison.OrdinalIgnoreCase)) && c.Value.Split(' ', ',', ';').Any(v => v.Equals(permission, StringComparison.OrdinalIgnoreCase)));
    }

    private string PermissionFor(string area, string action)
    {
        var normalized = area.Trim('/').ToLowerInvariant();
        if (normalized is "clientes" or "comercial/clientes") return action switch { "GET" => "comercial.clientes.visualizar", "POST" => "comercial.clientes.criar", "PUT" => "comercial.clientes.editar", "DELETE" => "comercial.clientes.inativar", _ => "comercial.clientes.visualizar" };
        if (normalized.Contains("propostas", StringComparison.Ordinal)) return action switch { "" => "comercial.propostas.visualizar", "APROVAR" => "comercial.propostas.aprovar", "REPROVAR" => "comercial.propostas.reprovar", "GERAR_PEDIDO" => "comercial.propostas.aprovar", _ => "comercial.propostas.criar" };
        if (normalized.Contains("pedidos", StringComparison.Ordinal)) return action switch { "" => "comercial.pedidos.visualizar", "CONFIRMAR" => "comercial.pedidos.confirmar", "CANCELAR" => "comercial.pedidos.cancelar", "GERAR_OS" => "comercial.pedidos.confirmar", _ => "comercial.pedidos.visualizar" };
        if (normalized.Contains("produtos", StringComparison.Ordinal)) return action switch { "POST" => "estoque.produtos.criar", "PUT" => "estoque.produtos.editar", "DELETE" => "estoque.produtos.inativar", _ => "estoque.produtos.visualizar" };
        if (normalized.Contains("fornecedores", StringComparison.Ordinal)) return action switch { "POST" => "compras.fornecedores.criar", "PUT" => "compras.fornecedores.editar", _ => "compras.fornecedores.visualizar" };
        if (normalized.Contains("ativos", StringComparison.Ordinal)) return action switch { "POST" => "industrial.ativos.criar", "PUT" => "industrial.ativos.editar", _ => "industrial.ativos.visualizar" };
        if (normalized.Contains("ordens", StringComparison.Ordinal)) return action switch { "" => "os.ordens.visualizar", "INICIAR" => "os.ordens.iniciar", "CONCLUIR" => "os.ordens.concluir", "CANCELAR" => "os.ordens.cancelar", "AGENDAR" => "os.ordens.agendar", "PAUSAR" => "os.ordens.pausar", _ => "os.ordens.criar" };
        return action == "CSV" ? "enterprise.relatorios.exportar" : "enterprise.relatorios.exportar";
    }

    private static string NormalizeEnterpriseArea(string area, string prefix = "enterprise") => prefix == "industria" ? $"industria/{area}" : area.Contains('/') ? area : $"comercial/{area}";

    private static string SanitizeCsv(string? value) => (value ?? string.Empty).Replace(";", ",", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal);

    private string Area() => Request.Path.Value?.Trim('/').Replace("api/", string.Empty, StringComparison.OrdinalIgnoreCase) ?? "enterprise";

    private string CorrelationId() => HttpContext.TraceIdentifier;
}
