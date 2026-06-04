using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers.Saas;

[ApiController]
[Route("api/saas/admin")]
public sealed class SaasAdminController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ITenantProvisioningService _provisioningService;

    public SaasAdminController(DapperContext context, ITenantProvisioningService provisioningService)
    {
        _context = context;
        _provisioningService = provisioningService;
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantInfo>>>> Tenants(CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        const string sql = "select id, nome, slug, status, ambiente from sigov.tenant where is_deleted = false order by nome;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantInfo>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantInfo>>.Ok(rows.AsList()));
    }

    [HttpGet("tenants/{id:long}")]
    public async Task<ActionResult<ApiResponse<TenantInfo>>> Tenant(long id, CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TenantInfo>(new CommandDefinition("select id, nome, slug, status, ambiente from sigov.tenant where id = @Id and is_deleted = false;", new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<TenantInfo>.Fail("Tenant não encontrado.")) : Ok(ApiResponse<TenantInfo>.Ok(row));
    }

    [HttpPost("tenants/provisionar")]
    public async Task<ActionResult<ApiResponse<ProvisionTenantResult>>> Provisionar(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        var result = await _provisioningService.ProvisionarAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<ProvisionTenantResult>.Ok(result));
    }

    [HttpPost("tenants/{id:long}/suspender")]
    public Task<ActionResult<ApiResponse<object>>> Suspender(long id, CancellationToken cancellationToken) => AlterarStatus(id, "SUSPENSO", cancellationToken);

    [HttpPost("tenants/{id:long}/reativar")]
    public Task<ActionResult<ApiResponse<object>>> Reativar(long id, CancellationToken cancellationToken) => AlterarStatus(id, "ATIVO", cancellationToken);

    [HttpPost("tenants/{id:long}/cancelar")]
    public Task<ActionResult<ApiResponse<object>>> Cancelar(long id, CancellationToken cancellationToken) => AlterarStatus(id, "CANCELADO", cancellationToken);

    [HttpGet("planos")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Planos(CancellationToken cancellationToken) => ListarCatalogo("plano_saas", cancellationToken);

    [HttpGet("modulos")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Modulos(CancellationToken cancellationToken) => ListarCatalogo("modulo_saas", cancellationToken);

    [HttpGet("tenants/{id:long}/uso")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Uso(long id, CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<object>(new CommandDefinition("select ano, mes, usuarios_ativos, requisicoes_api, armazenamento_bytes from sigov.tenant_uso_mensal where tenant_id = @Id order by ano desc, mes desc;", new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarStatus(long id, string status, CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("update sigov.tenant set status = @Status, updated_at = now() where id = @Id;", new { Id = id, Status = status }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { id, status }));
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> ListarCatalogo(string tabela, CancellationToken cancellationToken)
    {
        if (!IsSigovAdmin())
        {
            return Forbid();
        }

        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<object>(new CommandDefinition($"select * from sigov.{tabela} where ativo = true order by id;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }

    private bool IsSigovAdmin()
    {
        return User.IsInRole("SIGOV_ADMIN")
            || string.Equals(User.FindFirst("tipo_usuario")?.Value, "SIGOV_ADMIN", StringComparison.OrdinalIgnoreCase)
            || (HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                && string.Equals(Request.Headers["X-Sigov-Admin"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase));
    }
}
