using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas/tenants")]
public sealed class SaasTenantsController : ControllerBase
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<SaasTenantsController> _logger;

    public SaasTenantsController(NpgsqlConnectionFactory connectionFactory, ILogger<SaasTenantsController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Listar([FromQuery] string? busca, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
            const string countSql = @"select count(*) from sigov.tenant where is_deleted = false and (@Busca is null or nome ilike '%' || @Busca || '%' or slug ilike '%' || @Busca || '%' or coalesce(documento,'') ilike '%' || @Busca || '%');";
            const string dataSql = @"select id, nome, slug as codigo, documento, email, telefone, coalesce(plano, metadados->>'plano', 'global') as plano, cor_primaria as corPrimaria, logo_url as logoUrl, ativo
from sigov.tenant
where is_deleted = false and (@Busca is null or nome ilike '%' || @Busca || '%' or slug ilike '%' || @Busca || '%' or coalesce(documento,'') ilike '%' || @Busca || '%')
order by nome offset @Offset limit @PageSize;";
            using var connection = _connectionFactory.CreateConnection();
            var args = new { Busca = string.IsNullOrWhiteSpace(busca) ? null : busca.Trim(), Offset = (page - 1) * pageSize, PageSize = pageSize };
            var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(countSql, args, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var rows = (await connection.QueryAsync<TenantRow>(new CommandDefinition(dataSql, args, cancellationToken: cancellationToken)).ConfigureAwait(false))
                .Select(t => t with { Documento = MaskDocument(t.Documento), Email = MaskEmail(t.Email), Telefone = MaskPhone(t.Telefone) })
                .ToArray();
            return Ok(ApiResponse<object>.Ok(new { page, pageSize, total, items = rows }, correlationId: CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar tenants. CorrelationId={CorrelationId}", CorrelationId());
            return StatusCode(500, ApiResponse<object>.Fail("Não foi possível listar tenants.", CorrelationId()));
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<TenantRow>>> Obter(long id, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var row = await connection.QuerySingleOrDefaultAsync<TenantRow>(new CommandDefinition("select id, nome, slug as codigo, documento, email, telefone, coalesce(plano, metadados->>'plano', 'global') as plano, cor_primaria as corPrimaria, logo_url as logoUrl, ativo from sigov.tenant where id=@id and is_deleted=false", new { id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return row is null ? NotFound(ApiResponse<TenantRow>.Fail("Tenant não encontrado.", CorrelationId())) : Ok(ApiResponse<TenantRow>.Ok(row, correlationId: CorrelationId()));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter tenant {Id}.", id); return StatusCode(500, ApiResponse<TenantRow>.Fail("Não foi possível obter tenant.", CorrelationId())); }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Criar([FromBody] TenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.Codigo)) return BadRequest(ApiResponse<object>.Fail("Nome e código são obrigatórios.", CorrelationId()));
        try
        {
            const string sql = @"insert into sigov.tenant(nome, nome_fantasia, documento, slug, status, ambiente, ativo, email, telefone, plano, cor_primaria, logo_url)
values(@Nome, @Nome, @Documento, @Codigo, 'ATIVO', 'DEVELOPMENT', @Ativo, @Email, @Telefone, @Plano, @CorPrimaria, @LogoUrl)
returning id;";
            using var connection = _connectionFactory.CreateConnection();
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, request, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await AuditarAsync(connection, "TENANT_CRIADO", id, null, request, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(Obter), new { id }, ApiResponse<object>.Ok(new { id }, "Tenant criado.", CorrelationId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar tenant.");
            return StatusCode(500, ApiResponse<object>.Fail("Não foi possível criar tenant. Verifique se o código já existe.", CorrelationId()));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] TenantRequest request, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(ApiResponse<object>.Fail("Id inválido.", CorrelationId()));
        try
        {
            const string sql = @"update sigov.tenant set nome=@Nome, documento=@Documento, email=@Email, telefone=@Telefone, plano=@Plano, cor_primaria=@CorPrimaria, logo_url=@LogoUrl, ativo=@Ativo, updated_at=now() where id=@Id and is_deleted=false;";
            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.Nome, request.Documento, request.Email, request.Telefone, request.Plano, request.CorPrimaria, request.LogoUrl, request.Ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (affected == 0) return NotFound(ApiResponse<object>.Fail("Tenant não encontrado.", CorrelationId()));
            await AuditarAsync(connection, "TENANT_ATUALIZADO", id, null, request, cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { id }, "Tenant atualizado.", CorrelationId()));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar tenant {Id}.", id); return StatusCode(500, ApiResponse<object>.Fail("Não foi possível atualizar tenant.", CorrelationId())); }
    }

    [HttpPatch("{id:long}/status")]
    public async Task<ActionResult<ApiResponse<object>>> Status(long id, [FromBody] TenantStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition("update sigov.tenant set ativo=@Ativo, status=case when @Ativo then 'ATIVO' else 'INATIVO' end, updated_at=now() where id=@Id and is_deleted=false", new { Id = id, request.Ativo }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (affected == 0) return NotFound(ApiResponse<object>.Fail("Tenant não encontrado.", CorrelationId()));
            await AuditarAsync(connection, "TENANT_STATUS_ALTERADO", id, null, request, cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { id, request.Ativo }, "Status alterado.", CorrelationId()));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar status tenant {Id}.", id); return StatusCode(500, ApiResponse<object>.Fail("Não foi possível alterar status.", CorrelationId())); }
    }

    private async Task AuditarAsync(System.Data.IDbConnection connection, string acao, long id, object? antes, object depois, CancellationToken ct)
    {
        const string sql = "insert into sigov.auditoria_evento(acao, entidade, entidade_id, ip, user_agent, antes, depois, correlation_id) values(@Acao, 'sigov.tenant', @Id, @Ip, @UserAgent, @Antes::jsonb, @Depois::jsonb, @CorrelationId::uuid);";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Acao = acao, Id = id.ToString(), Ip = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers["User-Agent"].ToString(), Antes = System.Text.Json.JsonSerializer.Serialize(antes), Depois = System.Text.Json.JsonSerializer.Serialize(depois), CorrelationId = Guid.Parse(CorrelationId()) }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private string CorrelationId() => HttpContext.Items[Middlewares.CorrelationIdMiddleware.HeaderName]?.ToString() ?? HttpContext.TraceIdentifier;
    private static string MaskDocument(string? v) => string.IsNullOrWhiteSpace(v) || v.Length < 5 ? "***" : $"{v[..2]}***{v[^2..]}";
    private static string MaskEmail(string? v) { if (string.IsNullOrWhiteSpace(v) || !v.Contains('@', StringComparison.Ordinal)) return "***"; var p = v.Split('@', 2); return $"{p[0][0]}***@{p[1]}"; }
    private static string MaskPhone(string? v) => string.IsNullOrWhiteSpace(v) || v.Length < 4 ? "***" : $"***{v[^4..]}";

    public sealed record TenantRow(long Id, string Nome, string Codigo, string? Documento, string? Email, string? Telefone, string Plano, string? CorPrimaria, string? LogoUrl, bool Ativo);
    public sealed record TenantRequest(string Nome, string Codigo, string? Documento, string? Email, string? Telefone, string? Plano, string? CorPrimaria, string? LogoUrl, bool Ativo);
    public sealed record TenantStatusRequest(bool Ativo);
}
