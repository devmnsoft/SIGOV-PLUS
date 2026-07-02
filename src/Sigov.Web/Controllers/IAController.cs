using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class IAController : Controller
{
    private readonly AiConfigurationService? _configurationService;
    private readonly AiAssistantService? _assistantService;
    private readonly ILogger<IAController>? _logger;

    public IAController() { }
    public IAController(AiConfigurationService configurationService, AiAssistantService assistantService, ILogger<IAController> logger)
    {
        _configurationService = configurationService;
        _assistantService = assistantService;
        _logger = logger;
    }

    [HttpGet("/Ia")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (_configurationService is null) return Configuracao();
        return View("Index", await _configurationService.GetStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("/Ia/Assistentes")]
    public async Task<IActionResult> Assistentes(CancellationToken cancellationToken) => _configurationService is null ? Assistente() : View("Index", await _configurationService.GetStatusAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("/Ia/Logs")]
    public async Task<IActionResult> Logs(CancellationToken cancellationToken) => _configurationService is null ? Execucoes() : View("Index", await _configurationService.GetStatusAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("/Ia/Politicas")]
    public IActionResult Politicas() => View("IaPage", new IaPageViewModel("Políticas de IA", "ia_politicas", "ia.politicas.visualizar", "/Ia/Politicas", "Governança LGPD, consentimento, auditoria e limites de uso."));

    [HttpPost("/Ia/Assistente/Sugerir")]
    [HttpPost("/Ia/Assistente/Resumo")]
    [HttpPost("/Ia/Assistente/Despacho")]
    [HttpPost("/Ia/Assistente/Checklist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssistentePost(AiRequestViewModel request, CancellationToken cancellationToken)
    {
        if (_assistantService is null) return Json(new { status = "Fallback honesto", message = "Assistente inteligente não configurado neste ambiente." });
        var result = await _assistantService.SuggestAsync(request, Request.Headers.UserAgent.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        return Json(result);
    }

    [HttpPost("/Ia/Resumo/Protocolo/{id:long}")]
    [HttpPost("/Ia/Resumo/Documento/{id:long}")]
    [HttpPost("/Ia/Resumo/Juridico/{id:long}")]
    [HttpPost("/Ia/Resumo/Contrato/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resumo(long id, CancellationToken cancellationToken)
    {
        var request = new AiRequestViewModel(RouteData.Values["controller"]?.ToString(), "resumo", $"Registro {id}", "Resumo solicitado pelo usuário com aviso LGPD.");
        var result = _assistantService is null ? new AiSuggestionResult("Fallback honesto", "Assistente inteligente não configurado neste ambiente.", "Não substitui parecer humano.", true, string.Empty) : await _assistantService.SuggestAsync(request, Request.Headers.UserAgent.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        return Json(result);
    }
    public IActionResult Dashboard() => View("IaPage", new IaPageViewModel("Dashboard IA", "ia_assistente", "ia.dashboard.visualizar", "/api/ia/dashboard", "Indicadores de consumo, sugestões, alertas, automações, documentos e relatórios."));
    public IActionResult Assistente() => View("IaPage", new IaPageViewModel("Assistente IA", "ia_assistente", "ia.assistente.acessar", "/api/ia/executar", "Pergunte ao assistente interno por módulo com contexto opcional e histórico auditado."));
    public IActionResult Sugestoes() => View("IaPage", new IaPageViewModel("Sugestões inteligentes", "ia_assistente", "ia.sugestoes.visualizar", "/api/ia/sugestoes", "Aprove, aplique ou rejeite sugestões rastreáveis."));
    public IActionResult Execucoes() => View("IaPage", new IaPageViewModel("Execuções IA", "ia_assistente", "ia.execucoes.visualizar", "/api/ia/execucoes", "Histórico de prompts, respostas, consumo e correlationId."));
    public IActionResult Automacoes() => View("IaPage", new IaPageViewModel("Automações IA", "ia_automacoes", "ia.automacoes.visualizar", "/api/ia/automacoes", "Cadastre gatilhos, condições, ações e confirmação humana para rotinas críticas."));
    public IActionResult Alertas() => View("IaPage", new IaPageViewModel("Alertas inteligentes", "ia_automacoes", "ia.alertas.visualizar", "/api/ia/alertas", "Alertas por prioridade, origem e status de resolução."));
    public IActionResult Predicoes() => View("IaPage", new IaPageViewModel("Predições IA", "ia_predicoes", "ia.predicoes.visualizar", "/api/ia/predicoes", "Riscos iniciais por regras para inadimplência, ruptura, atraso e contratos."));
    public IActionResult Documental() => View("IaPage", new IaPageViewModel("IA Documental", "ia_documental", "ia.documental.resumir", "/api/ia/documentos/{documentoId}/resumir", "Resumo, classificação, extração e revisão documental."));
    public IActionResult Relatorios() => View("IaPage", new IaPageViewModel("Relatórios por IA", "ia_relatorios", "ia.relatorios.gerar", "/api/ia/relatorios/gerar", "Geração assistida de relatórios com exportação textual."));
    public IActionResult Configuracao() => View("IaPage", new IaPageViewModel("Configuração IA", "ia_assistente", "ia.configuracao.visualizar", "/api/ia/configuracao", "Habilite IA, limites, provider padrão, envio externo e mascaramento LGPD."));
    public IActionResult Consumo() => View("IaPage", new IaPageViewModel("Consumo IA", "ia_assistente", "ia.consumo.visualizar", "/api/ia/consumo", "Interações, tokens, custo estimado, limites e integração com billing SaaS."));
}

public sealed record IaPageViewModel(string Title, string Module, string Permission, string ApiRoute, string Description);
