using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saude;

namespace Sigov.Web.Controllers;

public sealed class SaudeController : Controller
{
    public IActionResult Dashboard() => View(new SaudeDashboardViewModel());
    public IActionResult Unidades() => View(new UnidadeSaudeFormViewModel());
    public IActionResult Profissionais() => View(new ProfissionalSaudeFormViewModel());
    public IActionResult Pacientes() => View(new PacienteFormViewModel());
    public IActionResult PacienteDetalhe(long id) { ViewData["PacienteId"] = id; return View(); }
    public IActionResult Prontuario(long pacienteId) { ViewData["PacienteId"] = pacienteId; return View(); }
    public IActionResult Atendimentos() => View(new AtendimentoSaudeFormViewModel());
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
}
