using Microsoft.AspNetCore.Mvc;

using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

public sealed class GedController : Controller
{
    private readonly GedOperationalService _operationalDemo;
    private readonly ILogger<GedController> _operationalLogger;
    private readonly IAuditTrailService _auditTrail;

    public GedController(GedOperationalService operationalDemo, IAuditTrailService auditTrail, ILogger<GedController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _auditTrail = auditTrail;
        _operationalLogger = operationalLogger;
    }

    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Dashboard", null, cancellationToken));
    [Route("/Ged/Documentos")]
    public async Task<IActionResult> Documentos(CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Documentos", null, cancellationToken));
    public IActionResult Upload() => View();
    public IActionResult Pesquisa() => View();
    public IActionResult Workflow() => View();
    public IActionResult Historico() => View();
    public IActionResult AssinaturaTeste() => View();
    public IActionResult Contratos() => View();
    public IActionResult Tramitacoes() => View();
    public IActionResult Ocr() => View();


    [Route("/Ged")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Dashboard", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Ged/Pastas")]
    public async Task<IActionResult> Pastas(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Pastas", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/Pastas");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Em implantação", null, cancellationToken));
        }
    }

    [Route("/Ged/NovoDocumento")]
    public async Task<IActionResult> NovoDocumento(string? q = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "NovoDocumento", q, cancellationToken));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/NovoDocumento");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", "Em implantação", null, cancellationToken));
        }
    }
    [Route("/Ged/Detalhes/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _operationalDemo.BuildAsync("Ged", $"Detalhes #{id}", null, cancellationToken));
    [HttpPost, ValidateAntiForgeryToken, Route("/Ged/NovoDocumento")]
    public async Task<IActionResult> NovoDocumentoPost(CancellationToken cancellationToken) { await Audit("ged.documento.criar", null, cancellationToken); TempData["Warning"] = "Upload não executado sem storage e schema homologados."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Ged/{id:long}/NovaVersao")]
    public async Task<IActionResult> NovaVersao(long id, CancellationToken cancellationToken) { await Audit("ged.documento.nova_versao", id.ToString(), cancellationToken); TempData["Warning"] = "Nova versão não persistida sem tabela/arquivo homologados."; return Redirect($"/Ged/Detalhes/{id}"); }
    [HttpPost, ValidateAntiForgeryToken, Route("/Ged/{id:long}/Arquivar")]
    public async Task<IActionResult> Arquivar(long id, CancellationToken cancellationToken) { await Audit("ged.documento.arquivar", id.ToString(), cancellationToken); return Redirect($"/Ged/Detalhes/{id}"); }
    [Route("/Ged/{id:long}/Download")]
    [Route("/Ged/{id:long}/Visualizar")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken) { await Audit("ged.documento.acessar", id.ToString(), cancellationToken); TempData["Warning"] = "Arquivo indisponível: caminho físico não é exposto e storage real não foi confirmado."; return Redirect($"/Ged/Detalhes/{id}"); }
    private async Task Audit(string acao, string? id, CancellationToken ct) { try { await _auditTrail.RegistrarAsync(null, null, acao, "ged_documento", id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct); } catch (Exception ex) { _operationalLogger.LogWarning(ex, "Auditoria GED falhou"); } }
}
