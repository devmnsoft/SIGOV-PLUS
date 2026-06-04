using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/operacao")]
public sealed class OperacaoController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ITenantContext _tenantContext;

    public OperacaoController(DapperContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    [HttpGet("logs")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Logs(CancellationToken cancellationToken) => QueryTenantScopedAsync("select id, nivel, origem, mensagem, created_at from sigov.log_aplicacao where tenant_id = @TenantId order by created_at desc limit 100", cancellationToken);

    [HttpGet("erros")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Erros(CancellationToken cancellationToken) => QueryTenantScopedAsync("select id, origem, mensagem, tipo_excecao, created_at from sigov.log_erro where tenant_id = @TenantId order by created_at desc limit 100", cancellationToken);

    [HttpGet("health-historico")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> HealthHistorico(CancellationToken cancellationToken) => QueryGlobalAdminAsync("select nome, status, duracao_ms, created_at from sigov.health_check_historico order by created_at desc limit 100", cancellationToken);

    [HttpGet("metricas")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Metricas(CancellationToken cancellationToken) => QueryTenantScopedAsync("select nome, valor, tags, created_at from sigov.metric_snapshot where tenant_id = @TenantId order by created_at desc limit 100", cancellationToken);

    [HttpGet("eventos-seguranca")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> EventosSeguranca(CancellationToken cancellationToken) => QueryTenantScopedAsync("select tipo, severidade, detalhes, created_at from sigov.evento_seguranca where tenant_id = @TenantId order by created_at desc limit 100", cancellationToken);

    [HttpGet("backups")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Backups(CancellationToken cancellationToken) => QueryGlobalAdminAsync("select ambiente, arquivo, tamanho_bytes, checksum, status, iniciou_at, finalizou_at from sigov.backup_execucao order by iniciou_at desc limit 100", cancellationToken);

    [HttpGet("restores")]
    public Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> Restores(CancellationToken cancellationToken) => QueryGlobalAdminAsync("select ambiente, arquivo, status, iniciou_at, finalizou_at from sigov.restore_execucao order by iniciou_at desc limit 100", cancellationToken);

    private async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> QueryTenantScopedAsync(string sql, CancellationToken cancellationToken)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            return BadRequest(ApiResponse<IReadOnlyCollection<object>>.Fail("Tenant não resolvido."));
        }

        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<object>(new CommandDefinition(sql, new { TenantId = _tenantContext.TenantId.Value }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyCollection<object>>>> QueryGlobalAdminAsync(string sql, CancellationToken cancellationToken)
    {
        if (!HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() && !User.IsInRole("SIGOV_ADMIN"))
        {
            return Forbid();
        }

        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<object>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }
}
