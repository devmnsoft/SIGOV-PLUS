using Microsoft.AspNetCore.Mvc;

using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class GedController : Controller
{
    private readonly OperationalDemoService _operationalDemo;
    private readonly ILogger<GedController> _operationalLogger;

    public GedController(OperationalDemoService operationalDemo, ILogger<GedController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _operationalLogger = operationalLogger;
    }

    public IActionResult Dashboard() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Dashboard"));
    public IActionResult Documentos() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Documentos"));
    public IActionResult Upload() => View();
    public IActionResult Pesquisa() => View();
    public IActionResult Workflow() => View();
    public IActionResult Historico() => View();
    public IActionResult AssinaturaTeste() => View();
    public IActionResult Contratos() => View();
    public IActionResult Tramitacoes() => View();
    public IActionResult Ocr() => View();


    [Route("/Ged")]
    public IActionResult Index(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Dashboard", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Em implantação"));
        }
    }

    [Route("/Ged/Pastas")]
    public IActionResult Pastas(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Pastas", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/Pastas");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Em implantação"));
        }
    }

    [Route("/Ged/NovoDocumento")]
    public IActionResult NovoDocumento(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "NovoDocumento", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Ged/NovoDocumento");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Ged", "Em implantação"));
        }
    }
}
