using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Governanca;
using Sigov.Application.Abstractions;
using Sigov.Web.Models.Governanca;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class GovernancaTransversalController : Controller
{
    private readonly ITransversalGovernancaService _service;
    private readonly ICurrentTenant _tenant;
    private readonly ILogger<GovernancaTransversalController> _logger;
    public GovernancaTransversalController(ITransversalGovernancaService service, ICurrentTenant tenant, ILogger<GovernancaTransversalController> logger)
        => (_service, _tenant, _logger) = (service, tenant, logger);

    [HttpGet("/Pendencias")]
    public async Task<IActionResult> Pendencias(string? modulo, string? gravidade, int pagina = 1, int tamanho = 25, CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired("Central de Pendências", "pendencia");
        var data = await _service.ListarPendenciasAsync(modulo, gravidade, pagina, tamanho, ct).ConfigureAwait(false);
        return View("Central", Model("Central de Pendências", "Trabalho real em aberto no contexto e escopo autorizados.", "pendencia", modulo, gravidade, pagina, tamanho,
            data.Select(x => new CentralTransversalItem(x.Modulo, x.Titulo, x.Gravidade, x.Status, x.RotaAcao, x.Descricao, x.ResponsavelUsuarioId is null ? "Não atribuído" : $"Responsável #{x.ResponsavelUsuarioId}", x.Prazo))));
    }

    [HttpGet("/Alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct)
    {
        var data = await _service.ListarAlertasAsync(null, null, 1, 100, ct).ConfigureAwait(false);
        return View("Central", Model("Central de Alertas", "Riscos operacionais, de prazo, segurança, LGPD e integração.", "alerta", null, null, 1, 100, data.Select(x => new CentralTransversalItem(x.Modulo, x.Titulo, x.Severidade, x.Status, x.RotaAcao))));
    }

    [HttpGet("/Governanca/QualidadeDados")]
    public async Task<IActionResult> QualidadeDados(string? modulo, string? severidade, int pagina = 1, int tamanho = 25, CancellationToken ct = default)
    {
        if (!HasContext()) return ContextRequired("Qualidade de Dados", "qualidade");
        var data = await _service.ListarQualidadeAsync(modulo, severidade, pagina, tamanho, ct).ConfigureAwait(false);
        return View("Central", Model("Qualidade de Dados", "Diagnósticos persistidos e explicáveis; a correção é confirmada nos dados de origem.", "qualidade", modulo, severidade, pagina, tamanho,
            data.Select(x => new CentralTransversalItem(x.Modulo, x.Descricao, x.Severidade, x.Status, x.RotaCorrecao, $"Regra {x.Regra} · {x.Entidade} {x.EntidadeId}", VerificadoEm: x.DetectedAt))));
    }

    [HttpGet("/Governanca/IntegracoesInternas")]
    public async Task<IActionResult> IntegracoesInternas(CancellationToken ct)
    {
        var data = await _service.ListarIntegracoesAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Integrações Internas", "Eventos reais e integrações preparatórias identificadas explicitamente.", "integracao", null, null, 1, 100, data.Select(x => new CentralTransversalItem(x.Origem, x.Origem + " → " + x.Destino, x.Preparatoria ? "PREPARATÓRIA" : "REAL", x.Status, x.RotaCorrecao))));
    }

    [HttpGet("/QualidadeDados")]
    public IActionResult QualidadeDadosAlias() => RedirectToActionPermanent(nameof(QualidadeDados));

    [HttpGet("/IntegracoesInternas")]
    public IActionResult IntegracoesInternasAlias() => RedirectToActionPermanent(nameof(IntegracoesInternas));

    [HttpGet("/Modulos/StatusFuncional")]
    public async Task<IActionResult> StatusFuncional(CancellationToken ct)
    {
        var data = await _service.ListarStatusFuncionalAsync(ct).ConfigureAwait(false);
        return View("Central", Model("Status Funcional por Módulo", "Estado calculado por inspeção de estrutura; o que não foi comprovado permanece pendente.", "status", null, null, 1, 100, data.Select(x => new CentralTransversalItem(x.Modulo, x.Comprovacao, x.Dashboard ? "DASHBOARD" : "SEM DASHBOARD", x.StatusFinal, null))));
    }

    private bool HasContext() => _tenant.TenantId is > 0;
    private IActionResult ContextRequired(string title, string type)
    {
        _logger.LogWarning("Contexto operacional não selecionado. Route={Route} Actor={Actor} Stage={Stage} Result={Result} CorrelationId={CorrelationId}",
            Request.Path, User.Identity?.Name, "tenant-resolution", "selection-required", HttpContext.TraceIdentifier);
        return View("Central", Model(title, "Selecione um contexto autorizado antes de consultar dados operacionais.", type, null, null, 1, 25, []));
    }
    private CentralTransversalViewModel Model(string title, string description, string type, string? modulo, string? classificacao, int pagina, int tamanho, IEnumerable<CentralTransversalItem> items) =>
        new() { Titulo = title, Descricao = description, Tipo = type, ContextoSelecionado = HasContext(), ContextoNome = User.FindFirst("tenant_name")?.Value ?? $"Contexto #{_tenant.TenantId}", Modulo = modulo, Classificacao = classificacao, Pagina = Math.Max(1, pagina), Tamanho = Math.Clamp(tamanho, 1, 100), Itens = items.ToArray() };
}
