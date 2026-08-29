using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saude;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaudeController : Controller
{
    [HttpGet("/Saude")]
    public IActionResult Index() => View("Dashboard", new SaudeDashboardViewModel { Titulo = "Saúde Pública Municipal" });

    [HttpGet("/Saude/Dashboard")]
    public IActionResult Dashboard() => Index();

    [HttpGet("/Saude/Unidades")]
    public IActionResult Unidades() => View(new UnidadeSaudeFormViewModel());

    [HttpGet("/Saude/Unidades/Nova")]
    public IActionResult UnidadeNova() => View("Unidades", new UnidadeSaudeFormViewModel());

    [HttpGet("/Saude/Pacientes")]
    public IActionResult Pacientes() => View(new PacienteFormViewModel());

    [HttpGet("/Saude/Pacientes/Novo")]
    public IActionResult PacienteNovo() => View("Pacientes", new PacienteFormViewModel());

    [HttpGet("/Saude/Pacientes/Detalhe/{id:long}")]
    [HttpGet("/Saude/Pacientes/{id:long}")]
    public IActionResult PacienteDetalhe(long id) { ViewData["PacienteId"] = id; return View(); }

    [HttpGet("/Saude/Profissionais")]
    public IActionResult Profissionais() => View(new ProfissionalSaudeFormViewModel());

    [HttpGet("/Saude/Profissionais/Novo")]
    public IActionResult ProfissionalNovo() => View("Profissionais", new ProfissionalSaudeFormViewModel());

    [HttpGet("/Saude/Equipes")]
    public IActionResult Equipes() => Operacao("Equipes", "/api/saude/equipes", "Vínculos profissionais com vigência são auditados.");

    [HttpGet("/Saude/Agenda")]
    public IActionResult Agenda() => View(new AgendaSaudeFormViewModel());

    [HttpGet("/Saude/Agenda/Nova")]
    public IActionResult AgendaNova() => View("Agenda", new AgendaSaudeFormViewModel());

    [HttpGet("/Saude/Acolhimentos")]
    public IActionResult Acolhimentos() => Operacao("Acolhimentos e triagem", "/api/saude/acolhimentos", "Queixa, sinais vitais e classificação de risco protegidos.");

    [HttpGet("/Saude/Acolhimentos/Novo")]
    public IActionResult AcolhimentoNovo() => Acolhimentos();

    [HttpGet("/Saude/Atendimentos")]
    public IActionResult Atendimentos() => View(new AtendimentoSaudeFormViewModel());

    [HttpGet("/Saude/Atendimentos/Novo")]
    public IActionResult AtendimentoNovo() => View("Atendimentos", new AtendimentoSaudeFormViewModel());

    [HttpGet("/Saude/Atendimentos/Detalhe/{id:long}")]
    public IActionResult AtendimentoDetalhe(long id) { ViewData["AtendimentoId"] = id; return View("Prontuario"); }

    [HttpGet("/Saude/Prontuario/{pacienteId:long}")]
    public IActionResult Prontuario(long pacienteId) { ViewData["PacienteId"] = pacienteId; return View(); }

    [HttpGet("/Saude/Vacinacao")]
    public IActionResult Vacinacao() => View("Vacinacoes", new VacinacaoFormViewModel());

    [HttpGet("/Saude/Vacinacao/Nova")]
    public IActionResult VacinacaoNova() => Vacinacao();

    [HttpGet("/Saude/Farmacia")]
    public IActionResult Farmacia() => View(new FarmaciaProdutoFormViewModel());

    [HttpGet("/Saude/Farmacia/Dispensar")]
    public IActionResult Dispensar() => Operacao("Dispensação", "/api/saude/farmacia/dispensacoes", "Saldo integrado e medicamento ativo são validados antes da dispensação.");

    [HttpGet("/Saude/Regulacao")]
    public IActionResult Regulacao() => View(new RegulacaoFormViewModel());

    [HttpGet("/Saude/Regulacao/Novo")]
    public IActionResult RegulacaoNova() => Regulacao();

    [HttpGet("/Saude/Procedimentos")]
    public IActionResult Procedimentos() => Operacao("Procedimentos", "/api/saude/procedimentos", "Produção por unidade, profissional e período.");

    public IActionResult Laboratorio() => View(new LaboratorioExameFormViewModel());
    public IActionResult AcsMicroareas() => View(new AcsMicroareaFormViewModel());
    public IActionResult AcsDomicilios() => View(new AcsDomicilioFormViewModel());
    public IActionResult AcsIndividuos() => View(new AcsIndividuoFormViewModel());
    public IActionResult AcsVisitas() => View(new AcsVisitaFormViewModel());
    public IActionResult AcsSync() => View();

    [HttpGet("/Saude/ACS/Domicilios/Create")]
    public IActionResult AcsDomicilioCreate() => View("AcsDomicilios", new AcsDomicilioFormViewModel());
    [HttpGet("/Saude/ACS/Domicilios/Edit/{id:long}")]
    public IActionResult AcsDomicilioEdit(long id) { ViewData["RegistroId"] = id; return View("AcsDomicilios", new AcsDomicilioFormViewModel()); }
    [HttpGet("/Saude/ACS/Domicilios/Details/{id:long}")]
    public IActionResult AcsDomicilioDetails(long id) { ViewData["RegistroId"] = id; return View("AcsDomicilios", new AcsDomicilioFormViewModel()); }
    [HttpGet("/Saude/ACS/Individuos/Create")]
    public IActionResult AcsIndividuoCreate() => View("AcsIndividuos", new AcsIndividuoFormViewModel());
    [HttpGet("/Saude/ACS/Individuos/Edit/{id:long}")]
    public IActionResult AcsIndividuoEdit(long id) { ViewData["RegistroId"] = id; return View("AcsIndividuos", new AcsIndividuoFormViewModel()); }
    [HttpGet("/Saude/ACS/Individuos/Details/{id:long}")]
    public IActionResult AcsIndividuoDetails(long id) { ViewData["RegistroId"] = id; return View("AcsIndividuos", new AcsIndividuoFormViewModel()); }
    [HttpGet("/Saude/ACS/Visitas/Create")]
    public IActionResult AcsVisitaCreate() => View("AcsVisitas", new AcsVisitaFormViewModel());
    [HttpGet("/Saude/ACS/Visitas/Edit/{id:long}")]
    public IActionResult AcsVisitaEdit(long id) { ViewData["RegistroId"] = id; return View("AcsVisitas", new AcsVisitaFormViewModel()); }
    [HttpGet("/Saude/ACS/Visitas/Details/{id:long}")]
    public IActionResult AcsVisitaDetails(long id) { ViewData["RegistroId"] = id; return View("AcsVisitas", new AcsVisitaFormViewModel()); }

    private IActionResult Operacao(string titulo, string endpoint, string descricao)
    {
        ViewData["Title"] = titulo;
        ViewData["Endpoint"] = endpoint;
        ViewData["Descricao"] = descricao;
        return View("OperacaoFunc06");
    }
}
