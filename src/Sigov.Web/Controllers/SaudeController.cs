using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saude;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaudeController : Controller
{
    [HttpGet("/Saude")]
    public IActionResult Index() => View("Dashboard", new SaudeDashboardViewModel { Titulo = "Saúde360 — Gestão Pública Multi-esfera" });

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

    [HttpGet("/Saude/Unidades/Create")] public IActionResult UnidadeCreate() => UnidadeNova();
    [HttpGet("/Saude/Unidades/Edit/{id:long}")] public IActionResult UnidadeEdit(long id) { ViewData["RegistroId"] = id; return Unidades(); }
    [HttpGet("/Saude/Unidades/Details/{id:long}")] public IActionResult UnidadeDetails(long id) { ViewData["RegistroId"] = id; return Unidades(); }
    [HttpGet("/Saude/Unidades/Equipes")] public IActionResult UnidadeEquipes() => Equipes();
    [HttpGet("/Saude/Unidades/Servicos")] public IActionResult UnidadeServicos() => Operacao("Serviços das unidades", "/api/saude/unidades/servicos", "Configure serviços somente para unidades ativas no contexto institucional selecionado.");
    [HttpGet("/Saude/Pacientes/Create")] public IActionResult PacienteCreate() => PacienteNovo();
    [HttpGet("/Saude/Pacientes/Edit/{id:long}")] public IActionResult PacienteEdit(long id) => PacienteDetalhe(id);
    [HttpGet("/Saude/Pacientes/Details/{id:long}")] public IActionResult PacienteDetails(long id) => PacienteDetalhe(id);
    [HttpGet("/Saude/Pacientes/Historico/{id:long}")] public IActionResult PacienteHistorico(long id) => PacienteDetalhe(id);
    [HttpGet("/Saude/Pacientes/Documentos/{id:long}")] public IActionResult PacienteDocumentos(long id) => PacienteDetalhe(id);
    [HttpGet("/Saude/ACS")] public IActionResult Acs() => Operacao("ACS360", "/api/saude/acs/visitas", "Selecione território, microárea, equipe e unidade; acessos sensíveis são auditados.");
    [HttpGet("/Saude/ACS/Territorios")] public IActionResult AcsTerritorios() => Operacao("Territórios ACS", "/api/saude/acs/microareas", "Territórios respeitam esfera, jurisdição e contexto institucional.");
    [HttpGet("/Saude/ACS/Visitas")] public IActionResult VisitasAcs() => AcsVisitas();
    [HttpGet("/Saude/ACS/Relatorios")] public IActionResult RelatoriosAcs() => Operacao("Relatórios ACS", "/api/saude/exportacoes/visitas-acs", "Exportações públicas são agregadas e arquivos identificáveis exigem permissão.");
    [HttpGet("/Saude/Fila")] public IActionResult Fila() => Operacao("Fila assistencial", "/api/saude/filas", "A ordem considera prioridade configurada e data de entrada.");
    [HttpGet("/Saude/Encaminhamentos")] public IActionResult Encaminhamentos() => Operacao("Encaminhamentos", "/api/saude/encaminhamentos", "Acompanhe origem, destino, prioridade e justificativa minimizada.");
    [HttpGet("/Saude/Regulacao/{area:regex(^(Solicitacoes|Fila|Prioridades|Autorizacoes|Relatorios)$)}")] public IActionResult RegulacaoArea(string area) => Operacao($"Regulação — {area}", $"/api/saude/regulacao/{area.ToLowerInvariant()}", "Dados clínicos são minimizados e o acesso é auditado.");
    [HttpGet("/Saude/Farmacia/{area:regex(^(Medicamentos|Estoque|Entradas|Dispensacoes|Lotes|Relatorios)$)}")] public IActionResult FarmaciaArea(string area) => Operacao($"Farmácia — {area}", $"/api/saude/farmacia/{area.ToLowerInvariant()}", "Movimentações são persistidas por lote e saldo negativo é bloqueado.");
    [HttpGet("/Saude/Vigilancia")] public IActionResult Vigilancia() => Operacao("Vigilância em Saúde", "/api/saude/vigilancia/notificacoes", "Informações públicas são agregadas; casos identificáveis exigem permissão específica.");
    [HttpGet("/Saude/Vigilancia/{area:regex(^(Notificacoes|Casos|Inspecoes|Campanhas|Relatorios)$)}")] public IActionResult VigilanciaArea(string area) => Operacao($"Vigilância — {area}", $"/api/saude/vigilancia/{area.ToLowerInvariant()}", "Use filtros territoriais sem expor informação clínica identificável.");

    private IActionResult Operacao(string titulo, string endpoint, string descricao)
    {
        ViewData["Title"] = titulo;
        ViewData["Endpoint"] = endpoint;
        ViewData["Descricao"] = descricao;
        return View("OperacaoFunc06");
    }
}
