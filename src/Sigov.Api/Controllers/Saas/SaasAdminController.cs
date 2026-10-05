using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Authorization;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers.Saas;

[ApiController]
[Route("api/saas/admin")]
public sealed class SaasAdminController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly IAuthorizationEvaluator _evaluator;

    public SaasAdminController(DapperContext context, ITenantProvisioningService provisioningService, IAuthorizationEvaluator evaluator)
    {
        _context = context;
        _provisioningService = provisioningService;
        _evaluator = evaluator;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> Tenants(CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        const string sql = "select id, nome, slug, status, ambiente from sigov.tenant where is_deleted = false order by nome;";
        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<TenantInfo>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantInfo>>.Ok(rows.AsList()));
    }

    [HttpGet("tenants/{id:long}")]
    public async Task<IActionResult> Tenant(long id, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        using var connection = _context.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TenantInfo>(new CommandDefinition("select id, nome, slug, status, ambiente from sigov.tenant where id = @Id and is_deleted = false;", new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<TenantInfo>.Fail("Tenant não encontrado.")) : Ok(ApiResponse<TenantInfo>.Ok(row));
    }

    [HttpPost("tenants/provisionar")]
    public async Task<IActionResult> Provisionar(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var result = await _provisioningService.ProvisionarAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<ProvisionTenantResult>.Ok(result));
    }

    [HttpPost("tenants/{id:long}/suspender")]
    public Task<IActionResult> Suspender(long id, CancellationToken cancellationToken) => AlterarStatus(id, "SUSPENSO", cancellationToken);

    [HttpPost("tenants/{id:long}/reativar")]
    public Task<IActionResult> Reativar(long id, CancellationToken cancellationToken) => AlterarStatus(id, "ATIVO", cancellationToken);

    [HttpPost("tenants/{id:long}/cancelar")]
    public Task<IActionResult> Cancelar(long id, CancellationToken cancellationToken) => AlterarStatus(id, "CANCELADO", cancellationToken);

    [HttpGet("planos")]
    public Task<IActionResult> Planos(CancellationToken cancellationToken) => ListarCatalogo("plano_saas", cancellationToken);

    [HttpGet("modulos")]
    public Task<IActionResult> Modulos(CancellationToken cancellationToken) => ListarCatalogo("modulo_saas", cancellationToken);

    [HttpGet("tenants/{id:long}/uso")]
    public async Task<IActionResult> Uso(long id, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        using var connection = _context.CreateConnection();
        var rows = await connection.QueryAsync<object>(new CommandDefinition("select ano, mes, usuarios_ativos, requisicoes_api, armazenamento_bytes from sigov.tenant_uso_mensal where tenant_id = @Id order by ano desc, mes desc;", new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }

    private async Task<IActionResult> AlterarStatus(long id, string status, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("update sigov.tenant set status = @Status, updated_at = now() where id = @Id;", new { Id = id, Status = status }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { id, status }));
    }

    private async Task<IActionResult> ListarCatalogo(string tabela, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        using var connection = _context.CreateConnection();
        var projection = tabela switch
        {
            "plano_saas" => "id, codigo, nome, descricao, valor_mensal, usuarios_inclusos, entidades_inclusas, armazenamento_gb, ativo, is_deleted, created_at, created_by, updated_at, updated_by, correlation_id",
            "modulo_saas" => "id, codigo, nome, descricao, categoria, ordem, rota_base, icone, ativo, is_deleted, created_at, created_by, updated_at, updated_by, correlation_id",
            _ => throw new ArgumentOutOfRangeException(nameof(tabela))
        };
        var rows = await connection.QueryAsync<object>(new CommandDefinition($"select {projection} from sigov.{tabela} where ativo = true order by id;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows.AsList()));
    }

    private async Task<AuthorizationDecision> EvaluateAdminAsync(CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("usuario_id");
        long? tenantId = long.TryParse(User.FindFirstValue("tenant_id"), out var tenantClaim) && tenantClaim > 0 ? tenantClaim : null;
        if (!long.TryParse(rawUserId, out var userId) || userId <= 0)
        {
            return AuthorizationDecision.Deny(AuthorizationDecisionReason.UsuarioInativoOuInexistente,
                new AuthorizationRequest(0, "saas", "plataforma", "administrar", tenantId,
                    CorrelationId: HttpContext.TraceIdentifier, Origem: "API_SAAS_ADMIN"), DateTimeOffset.UtcNow);
        }

        return await _evaluator.EvaluateAsync(new AuthorizationRequest(
            userId, "saas", "plataforma", "administrar", tenantId,
            CorrelationId: HttpContext.TraceIdentifier, Origem: "API_SAAS_ADMIN"), cancellationToken).ConfigureAwait(false);
    }

    private IActionResult ForbidAdmin(AuthorizationDecision decision) =>
        StatusCode(StatusCodes.Status403Forbidden, new
        {
            success = false,
            data = (object?)null,
            message = decision.Mensagem,
            correlationId = HttpContext.TraceIdentifier,
            motivo = "SEM_PERMISSAO"
        });

    private async Task<IActionResult?> RequireSigovAdminAsync(CancellationToken cancellationToken)
    {
        var decision = await EvaluateAdminAsync(cancellationToken).ConfigureAwait(false);
        return decision.Permitido ? null : ForbidAdmin(decision);
    }
}
