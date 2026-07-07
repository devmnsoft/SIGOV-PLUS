using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers.V1;

[Route("api/v1/protocolos")]
public sealed class ProtocolosApiController : ExternalV1Base
{
    private readonly DapperContext _db;
    public ProtocolosApiController(DapperContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] DateTime? de = null, [FromQuery] DateTime? ate = null, [FromQuery] string? numero = null, CancellationToken ct = default)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        var items = await cn.QueryAsync(new CommandDefinition(@"select id, numero, status, assunto, created_at as criadoEm
from sigov.protocolo
where tenant_id=@TenantId and is_deleted=false
  and (@Status is null or status=@Status)
  and (@Numero is null or numero ilike '%'||@Numero||'%')
  and (@De is null or created_at >= @De)
  and (@Ate is null or created_at <= @Ate)
order by created_at desc offset @Offset limit @Limit", new { TenantId = tenantId, Status = status, Numero = numero, De = de, Ate = ate, Offset = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Limit = Math.Clamp(pageSize, 1, 100) }, cancellationToken: ct));
        return OkEnvelope(new { page = Math.Max(1, page), pageSize = Math.Clamp(pageSize, 1, 100), items });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] ProtocoloCriarRequest? payload, CancellationToken ct)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        cn.Open();
        using var tx = cn.BeginTransaction();
        var ano = DateTime.UtcNow.Year;
        var seq = await cn.ExecuteScalarAsync<long>(new CommandDefinition("select count(*) + 1 from sigov.protocolo where tenant_id=@TenantId and exercicio=@Ano", new { TenantId = tenantId, Ano = ano }, tx, cancellationToken: ct));
        var numero = $"{ano}-{seq:000000}";
        var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.protocolo(tenant_id, numero, codigo, status, assunto, dados_json, correlation_id)
values(@TenantId,@Numero,@Numero,'ABERTO',@Assunto,jsonb_build_object('interessado',@Interessado),cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, Numero = numero, Assunto = string.IsNullOrWhiteSpace(payload?.Assunto) ? "Protocolo API" : payload?.Assunto, Interessado = payload?.Interessado, CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        var instanciaId = await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.workflow_instancia(tenant_id, protocolo_id, status, correlation_id) values(@TenantId,@Id,'ATIVO',cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, Id = id, CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition("insert into sigov.tarefa(tenant_id, protocolo_id, workflow_instancia_id, titulo, status, correlation_id) values(@TenantId,@Id,@InstanciaId,'Analisar protocolo','PENDENTE',cast(@CorrelationId as uuid)); insert into sigov.notificacao(tenant_id,titulo,mensagem,status,correlation_id) values(@TenantId,'Protocolo criado',@Msg,'ATIVA',cast(@CorrelationId as uuid)); insert into sigov.outbox_evento(tenant_id,evento,agregado,agregado_id,payload,status,correlation_id) values(@TenantId,'protocolo.criado','protocolo',@Id,jsonb_build_object('protocoloId',@Id,'numero',@Numero),'PENDENTE',cast(@CorrelationId as uuid));", new { TenantId = tenantId, Id = id, InstanciaId = instanciaId, Numero = numero, Msg = $"Protocolo {numero} criado.", CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        tx.Commit();
        return Created($"/api/v1/protocolos/{id}", new { id, numero, correlationId = CorrelationId });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obter(long id, CancellationToken ct)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        var protocolo = await cn.QuerySingleOrDefaultAsync(new CommandDefinition("select id, numero, status, assunto, created_at as criadoEm from sigov.protocolo where id=@Id and tenant_id=@TenantId and is_deleted=false", new { Id = id, TenantId = tenantId }, cancellationToken: ct));
        if (protocolo is null) return NotFound(new { error = "not_found", correlationId = CorrelationId });
        var movimentos = await cn.QueryAsync(new CommandDefinition("select id, observacao, created_at as criadoEm from sigov.protocolo_movimento where protocolo_id=@Id and tenant_id=@TenantId and is_deleted=false order by created_at", new { Id = id, TenantId = tenantId }, cancellationToken: ct));
        var tarefas = await cn.QueryAsync(new CommandDefinition("select id, titulo, status, concluida_at as concluidaEm from sigov.tarefa where protocolo_id=@Id and tenant_id=@TenantId and is_deleted=false order by created_at", new { Id = id, TenantId = tenantId }, cancellationToken: ct));
        return OkEnvelope(new { protocolo, movimentos, tarefas });
    }

    [HttpPost("{id:long}/tramitar")]
    public async Task<IActionResult> Tramitar(long id, [FromBody] ProtocoloTramitarRequest? payload, CancellationToken ct)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        await cn.ExecuteAsync(new CommandDefinition(@"insert into sigov.protocolo_movimento(tenant_id, protocolo_id, observacao, status, correlation_id) values(@TenantId,@Id,@Obs,'TRAMITADO',cast(@CorrelationId as uuid));
update sigov.tarefa set status='CONCLUIDA', concluida_at=now(), updated_at=now() where tenant_id=@TenantId and protocolo_id=@Id and concluida_at is null;
insert into sigov.tarefa(tenant_id, protocolo_id, titulo, status, correlation_id) values(@TenantId,@Id,'Nova análise do protocolo','PENDENTE',cast(@CorrelationId as uuid));
insert into sigov.notificacao(tenant_id,titulo,mensagem,status,correlation_id) values(@TenantId,'Protocolo tramitado','Protocolo tramitado com sucesso.','ATIVA',cast(@CorrelationId as uuid));
insert into sigov.outbox_evento(tenant_id,evento,agregado,agregado_id,payload,status,correlation_id) values(@TenantId,'protocolo.tramitado','protocolo',@Id,jsonb_build_object('protocoloId',@Id),'PENDENTE',cast(@CorrelationId as uuid));", new { TenantId = tenantId, Id = id, Obs = payload?.Observacao ?? "Tramitação via API", CorrelationId = GuidForDb() }, cancellationToken: ct));
        return OkEnvelope(new { id, status = "TRAMITADO" });
    }

    private string GuidForDb() => Guid.TryParse(CorrelationId, out var g) ? g.ToString() : Guid.NewGuid().ToString();
}

public sealed record ProtocoloCriarRequest(string? Assunto, string? Interessado);
public sealed record ProtocoloTramitarRequest(string? Observacao);
