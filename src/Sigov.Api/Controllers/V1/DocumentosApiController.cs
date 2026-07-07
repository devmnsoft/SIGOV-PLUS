using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers.V1;

[Route("api/v1/documentos")]
public sealed class DocumentosApiController : ExternalV1Base
{
    private readonly DapperContext _db;
    public DocumentosApiController(DapperContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        var items = await cn.QueryAsync(new CommandDefinition("select id, titulo, status, classificacao_lgpd as classificacaoLgpd, hash_sha256 as hashSha256, created_at as criadoEm from sigov.documento where tenant_id=@TenantId and is_deleted=false and (@Status is null or status=@Status) order by created_at desc offset @Offset limit @Limit", new { TenantId = tenantId, Status = status, Offset = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100), Limit = Math.Clamp(pageSize, 1, 100) }, cancellationToken: ct));
        return OkEnvelope(new { page, pageSize = Math.Clamp(pageSize, 1, 100), items });
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] DocumentoCriarRequest? payload, CancellationToken ct)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        var titulo = string.IsNullOrWhiteSpace(payload?.Titulo) ? "Documento API" : payload?.Titulo;
        var hash = Sha256($"{tenantId}:{titulo}:{payload?.ConteudoBase64}:{DateTimeOffset.UtcNow:O}");
        var storagePath = $"storage/tenant-{tenantId}/ged/{hash}.bin";
        using var cn = _db.CreateConnection();
        cn.Open();
        using var tx = cn.BeginTransaction();
        var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.documento(tenant_id,titulo,hash_sha256,storage_path,classificacao_lgpd,status,correlation_id) values(@TenantId,@Titulo,@Hash,@StoragePath,@Classificacao,'ATIVO',cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, Titulo = titulo, Hash = hash, StoragePath = storagePath, Classificacao = payload?.ClassificacaoLgpd ?? "INTERNO", CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition("insert into sigov.documento_versao(tenant_id,documento_id,versao,hash_sha256,storage_path,status,correlation_id) values(@TenantId,@Id,1,@Hash,@StoragePath,'ATIVO',cast(@CorrelationId as uuid)); insert into sigov.outbox_evento(tenant_id,evento,agregado,agregado_id,payload,status,correlation_id) values(@TenantId,'documento.criado','documento',@Id,jsonb_build_object('documentoId',@Id),'PENDENTE',cast(@CorrelationId as uuid));", new { TenantId = tenantId, Id = id, Hash = hash, StoragePath = storagePath, CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        if (string.Equals(payload?.ClassificacaoLgpd, "PUBLICO", StringComparison.OrdinalIgnoreCase))
        {
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.portal_validacao_documento(tenant_id,documento_id,codigo_publico,hash_publico,status,correlation_id) values(@TenantId,@Id,@Codigo,@Hash,'ATIVO',cast(@CorrelationId as uuid))", new { TenantId = tenantId, Id = id, Codigo = $"SIGOV-{id}-{hash[..8]}", Hash = hash, CorrelationId = GuidForDb() }, tx, cancellationToken: ct));
        }
        tx.Commit();
        return Created($"/api/v1/documentos/{id}", new { id, hashSha256 = hash, storagePath, correlationId = CorrelationId });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obter(long id, CancellationToken ct)
    {
        if (!long.TryParse(Tenant, out var tenantId)) return Unauthorized(new { error = "tenant_required", correlationId = CorrelationId });
        using var cn = _db.CreateConnection();
        var item = await cn.QuerySingleOrDefaultAsync(new CommandDefinition("select id, titulo, status, classificacao_lgpd as classificacaoLgpd, hash_sha256 as hashSha256, created_at as criadoEm from sigov.documento where id=@Id and tenant_id=@TenantId and is_deleted=false", new { Id = id, TenantId = tenantId }, cancellationToken: ct));
        return item is null ? NotFound(new { error = "not_found", correlationId = CorrelationId }) : OkEnvelope(item);
    }

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private string GuidForDb() => Guid.TryParse(CorrelationId, out var g) ? g.ToString() : Guid.NewGuid().ToString();
}

public sealed record DocumentoCriarRequest(string? Titulo, string? ConteudoBase64, string? ClassificacaoLgpd);
