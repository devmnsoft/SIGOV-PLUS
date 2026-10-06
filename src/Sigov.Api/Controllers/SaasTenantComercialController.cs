using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Saas.Comercial;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasTenantComercialController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ISaasAssinaturaComercialService _comercialService;
    private readonly IAuthorizationEvaluator _evaluator;
    private readonly ILogger<SaasTenantComercialController> _logger;

    public SaasTenantComercialController(
        DapperContext context,
        ISaasAssinaturaComercialService comercialService,
        IAuthorizationEvaluator evaluator,
        ILogger<SaasTenantComercialController> logger)
    {
        _context = context;
        _comercialService = comercialService;
        _evaluator = evaluator;
        _logger = logger;
    }

    [HttpGet("api/saas/tenants/{tenantId:long}/assinatura")]
    public async Task<ActionResult<ApiResponse<object>>> Assinatura(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant inválido.", cid));
            using var c = _context.CreateConnection();
            var assinatura = await c.QuerySingleOrDefaultAsync<object>(@"select a.*, p.codigo as plano_codigo, p.nome as plano_nome, p.ordem as plano_ordem, p.tipo_plano as plano_tipo
from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id
where a.tenant_id=@TenantId order by case when upper(a.status)='ATIVA' then 0 else 1 end, a.created_at desc limit 1", new { TenantId = tenantId });
            var historico = (await c.QueryAsync<object>("select id, assinatura_id, tenant_id, plano_anterior_id, plano_novo_id, acao, motivo, usuario_id, correlation_id, created_at from sigov.saas_assinatura_historico where tenant_id=@TenantId order by created_at desc limit 50", new { TenantId = tenantId })).ToList();
            return Ok(ApiResponse<object>.Ok(new { assinatura, historico }, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler assinatura do tenant {TenantId}.", tenantId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao consultar assinatura.", cid));
        }
    }

    [HttpGet("api/saas/tenants/{tenantId:long}/planos")]
    public async Task<ActionResult<ApiResponse<object>>> Planos(long tenantId)
    {
        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant inválido.", cid));
            using var c = _context.CreateConnection();
            var planos = (await c.QueryAsync<object>(
                @"select id, codigo, nome, descricao, tipo_plano, preco_base, moeda, periodicidade, limite_usuarios, limite_entidades, limite_armazenamento_mb,
permite_white_label, permite_dominio_customizado, destaque, ordem
from sigov.saas_plano where ativo order by ordem;", new { TenantId = tenantId })).ToList();
            return Ok(ApiResponse<object>.Ok(new { planos }, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar planos do tenant {TenantId}.", tenantId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao listar planos.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura")]
    public async Task<IActionResult> Criar(long tenantId, [FromBody] AssinaturaCriacaoRequest request, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;

        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0 || request is null || request.NovoPlanId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant e plano são obrigatórios.", cid));
            var resultado = await _comercialService.CriarAsync(tenantId, request.NovoPlanId, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("CRIAR assinatura tenant={TenantId} plano={Plano} sucesso={Sucesso} cid={Cid}", tenantId, request.NovoPlanId, resultado.Sucesso, cid);
            return resultado.Sucesso
                ? Ok(ApiResponse<object>.Ok(new { tenantId, mensagem = resultado.Mensagem }, cid))
                : BadRequest(ApiResponse<object>.Fail(resultado.Mensagem, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em CriarAssinatura.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao criar assinatura.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/upgrade")]
    public async Task<IActionResult> Upgrade(long tenantId, [FromBody] AssinaturaAlteracaoRequest request, CancellationToken cancellationToken) =>
        await TrocarPlano(tenantId, request, upgrade: true, cancellationToken);

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/downgrade")]
    public async Task<IActionResult> Downgrade(long tenantId, [FromBody] AssinaturaAlteracaoRequest request, CancellationToken cancellationToken) =>
        await TrocarPlano(tenantId, request, upgrade: false, cancellationToken);

    private async Task<IActionResult> TrocarPlano(long tenantId, AssinaturaAlteracaoRequest? request, bool upgrade, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;

        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0 || request is null || request.NovoPlanId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant e plano destino são obrigatórios.", cid));
            var resultado = upgrade
                ? await _comercialService.UpgradeAsync(tenantId, request.NovoPlanId, request.Motivo, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false)
                : await _comercialService.DowngradeAsync(tenantId, request.NovoPlanId, request.Motivo, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("{Operacao} assinatura tenant={TenantId} plano={Plano} sucesso={Sucesso} cid={Cid}", upgrade ? "UPGRADE" : "DOWNGRADE", tenantId, request.NovoPlanId, resultado.Sucesso, cid);
            return resultado.Sucesso
                ? Ok(ApiResponse<object>.Ok(new { tenantId, planoNovo = request.NovoPlanId, mensagem = resultado.Mensagem, modulosSuspensos = resultado.ModulosSuspensos ?? Array.Empty<string>() }, cid))
                : BadRequest(ApiResponse<object>.Fail(resultado.Mensagem, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em {Operacao}Assinatura.", upgrade ? "Upgrade" : "Downgrade");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao alterar plano.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/cancelar")]
    public async Task<IActionResult> Cancelar(long tenantId, [FromBody] AssinaturaCancelamentoRequest request, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;

        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0) return BadRequest(ApiResponse<object>.Fail("Tenant inválido.", cid));
            var resultado = await _comercialService.CancelarAsync(tenantId, request?.Motivo, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("CANCELAR assinatura tenant={TenantId} sucesso={Sucesso} cid={Cid}", tenantId, resultado.Sucesso, cid);
            return resultado.Sucesso
                ? Ok(ApiResponse<object>.Ok(new { tenantId, status = "CANCELADA", mensagem = resultado.Mensagem }, cid))
                : BadRequest(ApiResponse<object>.Fail(resultado.Mensagem, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em CancelarAssinatura.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao cancelar assinatura.", cid));
        }
    }

    [HttpPost("api/saas/tenants/{tenantId:long}/assinatura/status")]
    public async Task<IActionResult> Status(long tenantId, [FromBody] AssinaturaStatusRequest request, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;

        var cid = CorrelationId();
        try
        {
            if (tenantId <= 0 || request is null) return BadRequest(ApiResponse<object>.Fail("Tenant e status são obrigatórios.", cid));
            var target = request.Status?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(target)) return BadRequest(ApiResponse<object>.Fail("Status é obrigatório.", cid));
            if (target is not ("ATIVA" or "SUSPENSA" or "CANCELADA")) return BadRequest(ApiResponse<object>.Fail($"Status inválido: {target}.", cid));

            SaasComercialResultado resultado = target switch
            {
                "ATIVA" => await _comercialService.ReativarAsync(tenantId, request.Observacao, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false),
                "SUSPENSA" => await _comercialService.SuspenderAsync(tenantId, request.Observacao, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false),
                _ => await _comercialService.CancelarAsync(tenantId, request.Observacao, UsuarioId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false)
            };
            _logger.LogInformation("STATUS assinatura tenant={TenantId} status={Status} sucesso={Sucesso} cid={Cid}", tenantId, target, resultado.Sucesso, cid);
            return resultado.Sucesso
                ? Ok(ApiResponse<object>.Ok(new { tenantId, status = target, mensagem = resultado.Mensagem }, cid))
                : BadRequest(ApiResponse<object>.Fail(resultado.Mensagem, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em StatusAssinatura.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao alterar status da assinatura.", cid));
        }
    }

    private long UsuarioId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("usuario_id");
        return long.TryParse(raw, out var userId) ? userId : 0;
    }

    private async Task<AuthorizationDecision> EvaluateAdminAsync(CancellationToken cancellationToken)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("usuario_id");
        long? tenantId = long.TryParse(User.FindFirstValue("tenant_id"), out var tenantClaim) && tenantClaim > 0 ? tenantClaim : null;
        if (!long.TryParse(rawUserId, out var userId) || userId <= 0)
        {
            return AuthorizationDecision.Deny(AuthorizationDecisionReason.UsuarioInativoOuInexistente,
                new AuthorizationRequest(0, "saas", "plataforma", "administrar", tenantId,
                    CorrelationId: HttpContext.TraceIdentifier, Origem: "API_SAAS_COMERCIAL"), DateTimeOffset.UtcNow);
        }

        return await _evaluator.EvaluateAsync(new AuthorizationRequest(
            userId, "saas", "plataforma", "administrar", tenantId,
            CorrelationId: HttpContext.TraceIdentifier, Origem: "API_SAAS_COMERCIAL"), cancellationToken).ConfigureAwait(false);
    }

    private IActionResult ForbidAdmin(AuthorizationDecision decision) =>
        StatusCode(StatusCodes.Status403Forbidden, new
        {
            success = false,
            data = (object?)null,
            message = decision.Mensagem,
            correlationId = HttpContext.TraceIdentifier,
            motivo = decision.Motivo == AuthorizationDecisionReason.AlcadaInsuficiente
                ? SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.ForaEscopo)
                : SaasForbiddenMotivos.ToWire(SaasForbiddenMotivo.SemPermissao)
        });

    private async Task<IActionResult?> RequireSigovAdminAsync(CancellationToken cancellationToken)
    {
        var decision = await EvaluateAdminAsync(cancellationToken).ConfigureAwait(false);
        return decision.Permitido ? null : ForbidAdmin(decision);
    }

    private string CorrelationId() => HttpContext.TraceIdentifier;

    public sealed record AssinaturaCriacaoRequest(long NovoPlanId);
    public sealed record AssinaturaAlteracaoRequest(long NovoPlanId, string? Motivo);
    public sealed record AssinaturaCancelamentoRequest(string? Motivo);
    public sealed record AssinaturaStatusRequest(string? Status, string? Observacao);
}
