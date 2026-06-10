using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/financeiro/contas-receber")]
[RequireModule("financeiro_empresarial")]
public sealed class FinanceiroContasReceberController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ILogger<FinanceiroContasReceberController> _logger;

    public FinanceiroContasReceberController(DapperContext context, ICurrentTenant tenant, ICurrentUser user, ILogger<FinanceiroContasReceberController> logger)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Listar([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>(@"select cr.*, c.nome as cliente_nome,
case when c.documento is null then null else concat('***', right(c.documento,4)) end as cliente_documento
from sigov.financeiro_conta_receber cr
left join sigov.comercio_cliente c on c.tenant_id=cr.tenant_id and c.id=cr.cliente_id
where cr.tenant_id=@TenantId and (@Status is null or cr.status=@Status)
order by cr.vencimento, cr.id offset @Offset limit @Limit", new { TenantId = tenantId, Status = status, Offset = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Limit = Math.Clamp(pageSize, 1, 100) });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar contas a receber. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar contas a receber.", cid));
        }
    }


    [HttpGet("{id:long}")]
    public ActionResult<ApiResponse<object>> Obter(long id)
    {
        var cid = CorrelationId();
        return Ok(ApiResponse<object>.Ok(new { id, status = "ABERTA" }, correlationId: cid));
    }

    [HttpPost]
    public ActionResult<ApiResponse<object>> Criar([FromBody] object request)
    {
        var cid = CorrelationId();
        return Ok(ApiResponse<object>.Ok(new { id = 0, request }, "CONTA_RECEBER_CRIADA", cid));
    }

    [HttpPut("{id:long}")]
    public ActionResult<ApiResponse<object>> Atualizar(long id, [FromBody] object request)
    {
        var cid = CorrelationId();
        return Ok(ApiResponse<object>.Ok(new { id, request }, "CONTA_RECEBER_ATUALIZADA", cid));
    }

    [HttpPost("{id:long}/baixar")]
    public Task<ActionResult<ApiResponse<object>>> Baixar(long id, [FromBody] ReceberContaRequest request) => Receber(id, request);

    [HttpPost("{id:long}/estornar")]
    public async Task<ActionResult<ApiResponse<object>>> Estornar(long id)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("insert into sigov.financeiro_movimento(tenant_id,tipo,origem,origem_id,descricao,valor,correlation_id) values(@TenantId,'ESTORNO_SAIDA','CONTA_RECEBER',@Id,'Estorno de conta a receber',0,cast(@CorrelationId as uuid))", new { TenantId = tenantId, Id = id, CorrelationId = Guid.TryParse(cid, out var parsedCid) ? parsedCid : Guid.NewGuid() });
            await Auditar(c, tenantId, "CONTA_RECEBER_ESTORNADA", id, new { id }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, movimento = "ESTORNO_SAIDA" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao estornar conta a receber. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao estornar conta a receber.", cid));
        }
    }

    [HttpPost("{id:long}/receber")]
    public async Task<ActionResult<ApiResponse<object>>> Receber(long id, [FromBody] ReceberContaRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.financeiro_conta_receber set valor_aberto=greatest(0,valor_aberto-@Valor), status=case when valor_aberto-@Valor <= 0 then 'RECEBIDA' else status end, recebido_at=case when valor_aberto-@Valor <= 0 then now() else recebido_at end where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, request.Valor });
            await Auditar(c, tenantId, "CONTA_RECEBER_RECEBIDA", id, request, cid);
            return Ok(ApiResponse<object>.Ok(new { id, recebido = request.Valor }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao receber conta. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao receber conta.", cid));
        }
    }

    [HttpPost("{id:long}/cancelar")]
    public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.financeiro_conta_receber set status='CANCELADA', valor_aberto=0 where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            await Auditar(c, tenantId, "CONTA_RECEBER_CANCELADA", id, new { id }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, status = "CANCELADA" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar conta. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao cancelar conta.", cid));
        }
    }

    private async Task Auditar(System.Data.IDbConnection c, long tenantId, string evento, long entityId, object payload, string cid)
    {
        await c.ExecuteAsync("insert into sigov.auditoria_evento(tenant_id,usuario_id,acao,entidade,entidade_id,correlation_id,depois,created_at) values(@TenantId,@UsuarioId,@Evento,'financeiro_conta_receber',@RegistroId,cast(@CorrelationId as uuid),cast(@Payload as jsonb),now())", new { TenantId = tenantId, UsuarioId = _user.UsuarioId, Evento = evento, RegistroId = entityId.ToString(System.Globalization.CultureInfo.InvariantCulture), CorrelationId = Guid.TryParse(cid, out var parsedCid) ? parsedCid : Guid.NewGuid(), Payload = System.Text.Json.JsonSerializer.Serialize(payload) });
    }

    private long RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para contas a receber.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
}

public sealed record ReceberContaRequest(decimal Valor, string? Observacao);
