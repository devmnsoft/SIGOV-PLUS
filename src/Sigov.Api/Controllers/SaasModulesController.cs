using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;
using Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas")]
public sealed class SaasModulesController : ControllerBase
{
    private readonly IModuleAccessRepository _accessRepository;
    private readonly IModuleCatalogService _catalogService;
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<SaasModulesController> _logger;

    public SaasModulesController(IModuleCatalogService catalogService, IModuleAccessRepository accessRepository, NpgsqlConnectionFactory connectionFactory, ILogger<SaasModulesController> logger)
    {
        _catalogService = catalogService;
        _accessRepository = accessRepository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpGet("modulos")]
    public ActionResult<ApiResponse<IReadOnlyCollection<ModuleCatalogItem>>> GetModules()
    {
        try { return Ok(ApiResponse<IReadOnlyCollection<ModuleCatalogItem>>.Ok(_catalogService.GetModules(), correlationId: CorrelationId())); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar módulos."); return StatusCode(500, ApiResponse<IReadOnlyCollection<ModuleCatalogItem>>.Fail("Não foi possível listar módulos.", CorrelationId())); }
    }

    [HttpGet("modulos/{codigo}")]
    public ActionResult<ApiResponse<ModuleCatalogItem>> GetModule(string codigo)
    {
        try
        {
            var module = _catalogService.FindByCode(codigo);
            return module is null ? NotFound(ApiResponse<ModuleCatalogItem>.Fail("Módulo não encontrado.", CorrelationId())) : Ok(ApiResponse<ModuleCatalogItem>.Ok(module, correlationId: CorrelationId()));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter módulo {Codigo}.", codigo); return StatusCode(500, ApiResponse<ModuleCatalogItem>.Fail("Não foi possível obter módulo.", CorrelationId())); }
    }

    [HttpGet("tenants/{tenantId:long}/modulos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantModuleContract>>>> GetTenantModules(long tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _accessRepository.GetTenantModulesAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<IReadOnlyCollection<TenantModuleContract>>.Ok(rows, correlationId: CorrelationId()));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar módulos do tenant {TenantId}.", tenantId); return StatusCode(500, ApiResponse<IReadOnlyCollection<TenantModuleContract>>.Fail("Não foi possível listar módulos do tenant.", CorrelationId())); }
    }

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/habilitar")]
    public Task<ActionResult<ApiResponse<object>>> Enable(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "HABILITADO", "MODULO_ATIVADO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/ativar")]
    public Task<ActionResult<ApiResponse<object>>> Activate(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "HABILITADO", "MODULO_ATIVADO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/suspender")]
    public Task<ActionResult<ApiResponse<object>>> Suspend(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "SUSPENSO", "MODULO_DESATIVADO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/desativar")]
    public Task<ActionResult<ApiResponse<object>>> Deactivate(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "SUSPENSO", "MODULO_DESATIVADO", cancellationToken);

    [HttpPost("tenants/{tenantId:long}/modulos/{codigo}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> Cancel(long tenantId, string codigo, CancellationToken cancellationToken) => ChangeStatus(tenantId, codigo, "CANCELADO", "MODULO_DESATIVADO", cancellationToken);

    private async Task<ActionResult<ApiResponse<object>>> ChangeStatus(long tenantId, string codigo, string status, string auditAction, CancellationToken cancellationToken)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(codigo)) return BadRequest(ApiResponse<object>.Fail("Tenant e módulo são obrigatórios.", CorrelationId()));
        try
        {
            await _accessRepository.UpsertTenantModuleStatusAsync(tenantId, codigo, status, CurrentUserId(), CurrentCorrelationGuid(), cancellationToken).ConfigureAwait(false);
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition("insert into sigov.auditoria_evento(tenant_id, usuario_id, acao, entidade, entidade_id, ip, user_agent, depois, correlation_id) values(@TenantId, @UsuarioId, @Acao, 'sigov.tenant_modulo_contratado', @EntidadeId, @Ip, @UserAgent, jsonb_build_object('modulo', @Codigo, 'status', @Status), @CorrelationId);", new { TenantId = tenantId, UsuarioId = CurrentUserId(), Acao = auditAction, EntidadeId = codigo, Ip = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers["User-Agent"].ToString(), Codigo = codigo, Status = status, CorrelationId = CurrentCorrelationGuid() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { tenantId, codigo, status }, "Status do módulo alterado.", CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar módulo {Codigo} do tenant {TenantId}.", codigo, tenantId);
            return StatusCode(500, ApiResponse<object>.Fail("Não foi possível alterar status do módulo.", CorrelationId()));
        }
    }

    private long? CurrentUserId() => long.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("usuario_id")?.Value, out var id) ? id : null;
    private Guid CurrentCorrelationGuid() => Guid.TryParse(CorrelationId(), out var id) ? id : Guid.NewGuid();
    private string CorrelationId() => HttpContext.Items[Middlewares.CorrelationIdMiddleware.HeaderName]?.ToString() ?? HttpContext.TraceIdentifier;
}
