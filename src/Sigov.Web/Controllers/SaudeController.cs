using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saude;

using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class SaudeController : Controller
{
    private readonly OperationalDemoService _operationalDemo;
    private readonly ILogger<SaudeController> _operationalLogger;

    public SaudeController(OperationalDemoService operationalDemo, ILogger<SaudeController> operationalLogger)
    {
        _operationalDemo = operationalDemo;
        _operationalLogger = operationalLogger;
    }

    public IActionResult Dashboard() => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Dashboard"));
    [Route("/Saude/Unidades")]
    public IActionResult Unidades(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Unidades", q));
    public IActionResult Profissionais() => View(new ProfissionalSaudeFormViewModel());
    [Route("/Saude/Pacientes")]
    public IActionResult Pacientes(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Pacientes", q));
    public IActionResult PacienteDetalhe(long id) { ViewData["PacienteId"] = id; return View(); }
    public IActionResult Prontuario(long pacienteId) { ViewData["PacienteId"] = pacienteId; return View(); }
    [Route("/Saude/Atendimentos")]
    public IActionResult Atendimentos(string? q = null) => View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Atendimentos", q));
    public IActionResult Agenda() => View(new AgendaSaudeFormViewModel());
    public IActionResult Farmacia() => View(new FarmaciaProdutoFormViewModel());
    public IActionResult Vacinacoes() => View(new VacinacaoFormViewModel());
    public IActionResult Laboratorio() => View(new LaboratorioExameFormViewModel());
    public IActionResult Regulacao() => View(new RegulacaoFormViewModel());
    public IActionResult AcsMicroareas() => View(new AcsMicroareaFormViewModel());
    public IActionResult AcsDomicilios() => View(new AcsDomicilioFormViewModel());
    public IActionResult AcsIndividuos() => View(new AcsIndividuoFormViewModel());
    public IActionResult AcsVisitas() => View(new AcsVisitaFormViewModel());
    public IActionResult AcsSync() => View();


    [Route("/Saude")]
    public IActionResult Index(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Dashboard", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Saude/Index");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Em implantação"));
        }
    }

    [Route("/Saude/Agendas")]
    public IActionResult Agendas(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Agendas", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Saude/Agendas");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Em implantação"));
        }
    }

    [Route("/Saude/Acs")]
    public IActionResult Acs(string? q = null)
    {
        try
        {
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Acs", q));
        }
        catch (Exception ex)
        {
            _operationalLogger.LogError(ex, "Falha ao carregar fluxo Saude/Acs");
            TempData["Error"] = "Não foi possível carregar dados reais. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _operationalDemo.Build("Saude", "Em implantação"));
        }
    }
}
