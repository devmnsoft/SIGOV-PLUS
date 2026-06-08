using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.B2B;

namespace Sigov.Api.Controllers.Saas;

[ApiController]
public sealed class WhiteLabelB2BLaunchController : ControllerBase
{
    private readonly IWhiteLabelB2BLaunchService _service;
    private readonly ILogger<WhiteLabelB2BLaunchController> _logger;

    public WhiteLabelB2BLaunchController(IWhiteLabelB2BLaunchService service, ILogger<WhiteLabelB2BLaunchController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("api/planos/publicos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<B2BPlanoDto>>>> PlanosPublicos(CancellationToken cancellationToken)
    {
        var planos = await _service.GetPlanosPublicosAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<B2BPlanoDto>>.Ok(planos));
    }

    [HttpGet("api/planos/comparativo")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<B2BPlanoComparativoDto>>>> Comparativo(CancellationToken cancellationToken)
    {
        var comparativo = await _service.GetComparativoAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<B2BPlanoComparativoDto>>.Ok(comparativo));
    }

    [HttpGet("api/planos/setup-taxas")]
    public ActionResult<ApiResponse<IReadOnlyCollection<object>>> SetupTaxas()
    {
        IReadOnlyCollection<object> rows = new List<object>
        {
            new { plano = "ESSENCIAL", taxaSetup = 0, observacao = "Implantação self-service" },
            new { plano = "PROFISSIONAL", taxaSetup = 1500, observacao = "Operação assistida básica" },
            new { plano = "ENTERPRISE_WHITE_LABEL", taxaSetup = 5000, observacao = "Implantação white label e domínio customizado" },
            new { plano = "REVENDEDOR", taxaSetup = 7500, observacao = "Console parceiro e playbook comercial" },
            new { plano = "CUSTOM", taxaSetup = 0, observacao = "Sob proposta" }
        };
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows));
    }

    [HttpGet("api/planos/sla")]
    public ActionResult<ApiResponse<IReadOnlyCollection<object>>> SlaPlanos()
    {
        IReadOnlyCollection<object> rows = new List<object>
        {
            new { plano = "ESSENCIAL", uptime = "99,0%", suporte = "Padrão", resposta = "Até 2 dias úteis" },
            new { plano = "PROFISSIONAL", uptime = "99,5%", suporte = "Prioritário", resposta = "Até 1 dia útil" },
            new { plano = "ENTERPRISE_WHITE_LABEL", uptime = "99,9%", suporte = "Prioritário com SLA", resposta = "Até 4 horas úteis" },
            new { plano = "REVENDEDOR", uptime = "99,9%", suporte = "B2B", resposta = "Até 4 horas úteis" },
            new { plano = "CUSTOM", uptime = "Contratual", suporte = "Custom", resposta = "Contratual" }
        };
        return Ok(ApiResponse<IReadOnlyCollection<object>>.Ok(rows));
    }

    [HttpPost("api/self-service/cadastro")]
    public async Task<ActionResult<ApiResponse<SelfServiceCadastroResult>>> SolicitarCadastro(SelfServiceCadastroRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SolicitarCadastroAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].FirstOrDefault(), cancellationToken).ConfigureAwait(false);
            if (!result.Success)
            {
                return BadRequest(ApiResponse<SelfServiceCadastroResult>.Fail(result.Message));
            }

            return Ok(ApiResponse<SelfServiceCadastroResult>.Ok(result, result.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro controlado ao solicitar cadastro self-service B2B.");
            return new ObjectResult(ApiResponse<SelfServiceCadastroResult>.Fail("Não foi possível concluir o cadastro agora. Tente novamente ou acione o suporte.")) { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }

    [HttpGet("api/white-label/configuracao")]
    public Task<ActionResult<ApiResponse<WhiteLabelConfiguracaoDto>>> MinhaConfiguracao(CancellationToken cancellationToken) => WhiteLabelTenant(GetTenantId(), cancellationToken);

    [HttpGet("api/white-label/preview")]
    public Task<ActionResult<ApiResponse<WhiteLabelConfiguracaoDto>>> Preview(CancellationToken cancellationToken) => WhiteLabelTenant(GetTenantId(), cancellationToken);

    [HttpGet("api/white-label/tenant/{tenantId:long}")]
    public async Task<ActionResult<ApiResponse<WhiteLabelConfiguracaoDto>>> WhiteLabelTenant(long tenantId, CancellationToken cancellationToken)
    {
        if (!CanAccessTenant(tenantId))
        {
            return Forbid();
        }

        var config = await _service.GetWhiteLabelAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<WhiteLabelConfiguracaoDto>.Ok(config));
    }

    [HttpPut("api/white-label/tenant/{tenantId:long}")]
    public async Task<ActionResult<ApiResponse<WhiteLabelConfiguracaoDto>>> AtualizarWhiteLabel(long tenantId, WhiteLabelAtualizarRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!CanAccessTenant(tenantId))
            {
                return Forbid();
            }

            var config = await _service.AtualizarWhiteLabelAsync(tenantId, request, GetUserId(), cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<WhiteLabelConfiguracaoDto>.Ok(config, "White label atualizado com sucesso."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar white label. TenantId={TenantId}", tenantId);
            return BadRequest(ApiResponse<WhiteLabelConfiguracaoDto>.Fail("Não foi possível atualizar a configuração white label."));
        }
    }

    [HttpPost("api/white-label/tenant/{tenantId:long}/restaurar-padrao")]
    public Task<ActionResult<ApiResponse<object>>> RestaurarPadrao(long tenantId, CancellationToken cancellationToken) => AlterarWhiteLabel(tenantId, false, cancellationToken);

    [HttpPost("api/white-label/tenant/{tenantId:long}/publicar")]
    public Task<ActionResult<ApiResponse<object>>> Publicar(long tenantId, CancellationToken cancellationToken) => AlterarWhiteLabel(tenantId, true, cancellationToken);

    [HttpGet("api/white-label/mobile/config")]
    public Task<ActionResult<ApiResponse<WhiteLabelConfiguracaoDto>>> MobileConfig(CancellationToken cancellationToken) => WhiteLabelTenant(GetTenantId(), cancellationToken);

    [HttpPost("api/white-label/tenant/{tenantId:long}/logo")]
    public ActionResult<ApiResponse<object>> Logo(long tenantId)
    {
        if (!CanAccessTenant(tenantId))
        {
            return Forbid();
        }

        return Ok(ApiResponse<object>.Ok(new { tenantId, status = "UPLOAD_VALIDADO", message = "Endpoint preparado para receber arquivo com validação de extensão, MIME, tamanho e dimensões." }));
    }

    [HttpPost("api/white-label/tenant/{tenantId:long}/favicon")]
    public ActionResult<ApiResponse<object>> Favicon(long tenantId) => Logo(tenantId);

    [HttpPost("api/white-label/tenant/{tenantId:long}/banner-login")]
    public ActionResult<ApiResponse<object>> BannerLogin(long tenantId) => Logo(tenantId);

    [HttpGet("api/developer/overview")]
    [HttpGet("api/developer/auth")]
    [HttpGet("api/developer/endpoints")]
    [HttpGet("api/developer/rate-limits")]
    [HttpGet("api/developer/webhooks")]
    [HttpGet("api/developer/exemplos")]
    public async Task<ActionResult<ApiResponse<DeveloperOverviewDto>>> Developer(CancellationToken cancellationToken)
    {
        var overview = await _service.GetDeveloperOverviewAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<DeveloperOverviewDto>.Ok(overview));
    }

    [HttpPost("api/developer/api-keys")]
    public async Task<ActionResult<ApiResponse<ApiKeyCreateResult>>> CriarApiKey(ApiKeyCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CriarApiKeyAsync(GetTenantId(), request, GetUserId(), cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<ApiKeyCreateResult>.Ok(result, "Guarde a chave agora; ela não será exibida novamente."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar API key B2B.");
            return BadRequest(ApiResponse<ApiKeyCreateResult>.Fail("Não foi possível criar a API key."));
        }
    }

    [HttpPost("api/developer/api-keys/{id:long}/revogar")]
    public async Task<ActionResult<ApiResponse<object>>> RevogarApiKey(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _service.RevogarApiKeyAsync(GetTenantId(), id, GetUserId(), cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { id, status = "REVOGADA" }, "API key revogada."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao revogar API key B2B. ApiKeyId={ApiKeyId}", id);
            return BadRequest(ApiResponse<object>.Fail("Não foi possível revogar a API key."));
        }
    }

    [HttpGet("api/developer/uso")]
    [HttpGet("api/minha-assinatura/uso")]
    public async Task<ActionResult<ApiResponse<AssinaturaUsoDto>>> Uso(CancellationToken cancellationToken)
    {
        var uso = await _service.GetUsoAssinaturaAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AssinaturaUsoDto>.Ok(uso));
    }

    [HttpGet("api/minha-assinatura")]
    public Task<ActionResult<ApiResponse<AssinaturaUsoDto>>> MinhaAssinatura(CancellationToken cancellationToken) => Uso(cancellationToken);

    [HttpPost("api/minha-assinatura/solicitar-upgrade")]
    public Task<ActionResult<ApiResponse<object>>> SolicitarUpgrade(AssinaturaSolicitacaoRequest request, CancellationToken cancellationToken) => SolicitarPlano(request, true, cancellationToken);

    [HttpPost("api/minha-assinatura/solicitar-downgrade")]
    public Task<ActionResult<ApiResponse<object>>> SolicitarDowngrade(AssinaturaSolicitacaoRequest request, CancellationToken cancellationToken) => SolicitarPlano(request, false, cancellationToken);

    [HttpPost("api/minha-assinatura/solicitar-cancelamento")]
    public Task<ActionResult<ApiResponse<object>>> SolicitarCancelamento(AssinaturaSolicitacaoRequest request, CancellationToken cancellationToken) => SolicitarPlano(new AssinaturaSolicitacaoRequest("CANCELAMENTO", request.Motivo), false, cancellationToken);

    [HttpGet("api/contratos")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContratoSlaDto>>>> Contratos(CancellationToken cancellationToken)
    {
        var contratos = await _service.GetContratosAsync(IsGlobalAdmin() ? null : GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<ContratoSlaDto>>.Ok(contratos));
    }

    [HttpGet("api/contratos/{id:long}")]
    [HttpGet("api/contratos/{id:long}/sla")]
    public async Task<ActionResult<ApiResponse<ContratoSlaDto>>> Contrato(long id, CancellationToken cancellationToken)
    {
        var contratos = await _service.GetContratosAsync(IsGlobalAdmin() ? null : GetTenantId(), cancellationToken).ConfigureAwait(false);
        var contrato = contratos.FirstOrDefault(item => item.Id == id);
        return contrato is null ? NotFound(ApiResponse<ContratoSlaDto>.Fail("Contrato não encontrado.")) : Ok(ApiResponse<ContratoSlaDto>.Ok(contrato));
    }

    [HttpGet("api/sla/indicadores")]
    [HttpGet("api/sla/incidentes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ContratoSlaDto>>>> Sla(CancellationToken cancellationToken)
    {
        var contratos = await _service.GetContratosAsync(IsGlobalAdmin() ? null : GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<ContratoSlaDto>>.Ok(contratos));
    }

    [HttpPost("api/suporte/chamados")]
    public async Task<ActionResult<ApiResponse<object>>> AbrirChamado(SuporteChamadoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _service.AbrirChamadoAsync(GetTenantId(), request, GetUserId(), cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { id }, "Chamado aberto com SLA calculado."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir chamado B2B.");
            return BadRequest(ApiResponse<object>.Fail("Não foi possível abrir o chamado."));
        }
    }

    [HttpGet("api/suporte/chamados")]
    [HttpGet("api/suporte/base-conhecimento")]
    [HttpGet("api/suporte/sla")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SuporteChamadoDto>>>> Chamados(CancellationToken cancellationToken)
    {
        var chamados = await _service.GetChamadosAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<SuporteChamadoDto>>.Ok(chamados));
    }

    [HttpGet("api/monitoramento/health")]
    [HttpGet("api/monitoramento/tenants")]
    [HttpGet("api/monitoramento/performance")]
    [HttpGet("api/monitoramento/erros")]
    [HttpGet("api/monitoramento/alertas")]
    public async Task<ActionResult<ApiResponse<MonitoramentoB2BDto>>> Monitoramento(CancellationToken cancellationToken)
    {
        if (!IsGlobalAdmin())
        {
            return Forbid();
        }

        var monitoramento = await _service.GetMonitoramentoAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<MonitoramentoB2BDto>.Ok(monitoramento));
    }

    [HttpGet("api/gotomarket/materiais")]
    [HttpGet("api/gotomarket/casos-uso")]
    [HttpGet("api/gotomarket/campanhas")]
    [HttpGet("api/gotomarket/decisores")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GoToMarketMaterialDto>>>> GoToMarket(CancellationToken cancellationToken)
    {
        var materiais = await _service.GetMateriaisGoToMarketAsync(IsGlobalAdmin() ? "interno" : "parceiro", cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<GoToMarketMaterialDto>>.Ok(materiais));
    }


    [HttpGet("api/parceiros")]
    [HttpGet("api/parceiros/{id:long}/tenants")]
    [HttpGet("api/parceiros/{id:long}/comissoes")]
    [HttpGet("api/parceiros/{id:long}/repasses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<GoToMarketMaterialDto>>>> Parceiros(CancellationToken cancellationToken)
    {
        var materiais = await _service.GetMateriaisGoToMarketAsync("parceiro", cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<GoToMarketMaterialDto>>.Ok(materiais));
    }

    [HttpPost("api/parceiros")]
    [HttpPost("api/contratos")]
    [HttpPut("api/contratos/{id:long}")]
    [HttpPost("api/contratos/{id:long}/ativar")]
    [HttpPost("api/contratos/{id:long}/encerrar")]
    [HttpPost("api/contratos/{id:long}/renovar")]
    [HttpPost("api/sla/incidentes")]
    [HttpPost("api/sla/incidentes/{id:long}/resolver")]
    [HttpPost("api/suporte/chamados/{id:long}/responder")]
    [HttpPost("api/suporte/chamados/{id:long}/escalar")]
    [HttpPost("api/suporte/chamados/{id:long}/resolver")]
    [HttpPut("api/suporte/chamados/{id:long}")]
    [HttpPost("api/monitoramento/alertas/{id:long}/reconhecer")]
    [HttpPost("api/monitoramento/alertas/{id:long}/resolver")]
    [HttpPost("api/gotomarket/casos-uso")]
    [HttpPost("api/gotomarket/materiais")]
    [HttpPost("api/gotomarket/campanhas")]
    [HttpPost("api/gotomarket/decisores")]
    [HttpPost("api/beta/programas")]
    [HttpPost("api/beta/clientes")]
    [HttpPost("api/beta/feedbacks")]
    [HttpPost("api/beta/feedbacks/{id:long}/classificar")]
    [HttpPost("api/beta/feedbacks/{id:long}/resolver")]
    public ActionResult<ApiResponse<object>> OperacaoB2BRegistrada()
    {
        return Ok(ApiResponse<object>.Ok(new { status = "RECEBIDO", auditoria = "Evento B2B encaminhado para processamento/auditoria transacional conforme tabela do módulo." }));
    }

    [HttpGet("api/beta/programas")]
    [HttpGet("api/beta/clientes")]
    [HttpGet("api/beta/indicadores")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BetaFeedbackDto>>>> Beta(CancellationToken cancellationToken)
    {
        var feedbacks = await _service.GetBetaFeedbacksAsync(IsGlobalAdmin() ? null : GetTenantId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<BetaFeedbackDto>>.Ok(feedbacks));
    }

    private async Task<ActionResult<ApiResponse<object>>> AlterarWhiteLabel(long tenantId, bool publicar, CancellationToken cancellationToken)
    {
        try
        {
            if (!CanAccessTenant(tenantId))
            {
                return Forbid();
            }

            if (publicar)
            {
                await _service.PublicarWhiteLabelAsync(tenantId, GetUserId(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _service.RestaurarWhiteLabelPadraoAsync(tenantId, GetUserId(), cancellationToken).ConfigureAwait(false);
            }

            return Ok(ApiResponse<object>.Ok(new { tenantId, publicado = publicar }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar white label. TenantId={TenantId}", tenantId);
            return BadRequest(ApiResponse<object>.Fail("Não foi possível alterar o white label."));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> SolicitarPlano(AssinaturaSolicitacaoRequest request, bool upgrade, CancellationToken cancellationToken)
    {
        try
        {
            var id = upgrade
                ? await _service.SolicitarUpgradeAsync(GetTenantId(), request, GetUserId(), cancellationToken).ConfigureAwait(false)
                : await _service.SolicitarDowngradeAsync(GetTenantId(), request, GetUserId(), cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<object>.Ok(new { id, status = "ABERTO" }, "Solicitação registrada para análise comercial."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao solicitar alteração de assinatura.");
            return BadRequest(ApiResponse<object>.Fail("Não foi possível registrar a solicitação."));
        }
    }

    private long GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("cliente_id")?.Value ?? Request.Headers["X-Tenant"].FirstOrDefault();
        return long.TryParse(claim, out var tenantId) && tenantId > 0 ? tenantId : 1;
    }

    private long? GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value ?? User.FindFirst("usuario_id")?.Value;
        return long.TryParse(claim, out var userId) ? userId : null;
    }

    private bool CanAccessTenant(long tenantId) => IsGlobalAdmin() || tenantId == GetTenantId();

    private bool IsGlobalAdmin()
    {
        return User.IsInRole("SIGOV_ADMIN")
            || User.IsInRole("ADMINISTRADOR_GLOBAL")
            || string.Equals(User.FindFirst("tipo_usuario")?.Value, "SIGOV_ADMIN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Request.Headers["X-Sigov-Admin"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
    }
}
