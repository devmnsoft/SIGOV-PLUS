using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Core;
using Sigov.Application.Saude;
using Sigov.Web.Models.Saude;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaudeController : Controller
{
    private readonly IUnidadeSaudeService _unidadeService;
    private readonly IProfissionalSaudeService _profissionalService;
    private readonly IPacienteService _pacienteService;
    private readonly IAgendaSaudeService _agendaService;
    private readonly IAtendimentoSaudeService _atendimentoService;
    private readonly IVacinacaoService _vacinacaoService;
    private readonly IFarmaciaService _farmaciaService;
    private readonly IRegulacaoService _regulacaoService;
    private readonly ISaudeDashboardService _dashboardService;
    private readonly IProntuarioService _prontuarioService;
    private readonly IPessoaCadastroService _pessoaService;

    public SaudeController(
        IUnidadeSaudeService unidadeService,
        IProfissionalSaudeService profissionalService,
        IPacienteService pacienteService,
        IAgendaSaudeService agendaService,
        IAtendimentoSaudeService atendimentoService,
        IVacinacaoService vacinacaoService,
        IFarmaciaService farmaciaService,
        IRegulacaoService regulacaoService,
        ISaudeDashboardService dashboardService,
        IProntuarioService prontuarioService,
        IPessoaCadastroService pessoaService)
    {
        _unidadeService = unidadeService;
        _profissionalService = profissionalService;
        _pacienteService = pacienteService;
        _agendaService = agendaService;
        _atendimentoService = atendimentoService;
        _vacinacaoService = vacinacaoService;
        _farmaciaService = farmaciaService;
        _regulacaoService = regulacaoService;
        _dashboardService = dashboardService;
        _prontuarioService = prontuarioService;
        _pessoaService = pessoaService;
    }

    [HttpGet("/Saude")]
    [HttpGet("/Saude/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var dashResult = await _dashboardService.ObterAsync(ct).ConfigureAwait(false);
        ViewBag.DashboardReal = dashResult.IsSuccess ? dashResult.Value : null;
        return View("Dashboard", new SaudeDashboardViewModel { Titulo = "Saúde360 — Gestão Pública Multi-esfera" });
    }

    [HttpGet("/Saude/Unidades")]
    [HttpGet("/Saude/Unidades/Nova")]
    public async Task<IActionResult> Unidades(CancellationToken ct)
    {
        var res = await _unidadeService.ListarAsync(new UnidadeSaudeFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Unidades = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<UnidadeSaudeResponse>();
        return View("Unidades", new UnidadeSaudeFormViewModel());
    }

    [HttpPost("/Saude/Unidades/Nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnidadeNovaPost([FromForm] UnidadeSaudeCreateRequest request, CancellationToken ct)
    {
        var res = await _unidadeService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao cadastrar unidade.";
        else
            TempData["ToastOk"] = "Unidade de saúde cadastrada com sucesso.";
        return RedirectToAction(nameof(Unidades));
    }

    [HttpGet("/Saude/Pacientes")]
    [HttpGet("/Saude/Pacientes/Novo")]
    public async Task<IActionResult> Pacientes(CancellationToken ct)
    {
        await CarregarPessoasAsync(ct).ConfigureAwait(false);
        var res = await _pacienteService.ListarAsync(new PacienteFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Pacientes = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<PacienteResumoResponse>();
        return View("Pacientes", new PacienteFormViewModel());
    }

    [HttpPost("/Saude/Pacientes/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PacienteNovoPost([FromForm] PacienteCreateRequest request, CancellationToken ct)
    {
        var res = await _pacienteService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao cadastrar paciente.";
        else
            TempData["ToastOk"] = "Paciente cadastrado com sucesso.";
        return RedirectToAction(nameof(Pacientes));
    }

    [HttpGet("/Saude/Pacientes/Detalhe/{id:long}")]
    [HttpGet("/Saude/Pacientes/{id:long}")]
    public async Task<IActionResult> PacienteDetalhe(long id, CancellationToken ct)
    {
        ViewData["PacienteId"] = id;
        var res = await _pacienteService.ObterAsync(id, ct).ConfigureAwait(false);
        ViewBag.Paciente = res.IsSuccess ? res.Value : null;
        return View("PacienteDetalhe");
    }

    [HttpGet("/Saude/Profissionais")]
    [HttpGet("/Saude/Profissionais/Novo")]
    public async Task<IActionResult> Profissionais(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        await CarregarPessoasAsync(ct).ConfigureAwait(false);
        var res = await _profissionalService.ListarAsync(new ProfissionalSaudeFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Profissionais = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<ProfissionalSaudeResponse>();
        return View("Profissionais", new ProfissionalSaudeFormViewModel());
    }

    [HttpPost("/Saude/Profissionais/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfissionalNovoPost([FromForm] ProfissionalSaudeCreateRequest request, CancellationToken ct)
    {
        var res = await _profissionalService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao cadastrar profissional.";
        else
            TempData["ToastOk"] = "Profissional de saúde cadastrado com sucesso.";
        return RedirectToAction(nameof(Profissionais));
    }

    [HttpGet("/Saude/Equipes")]
    public IActionResult Equipes() => Operacao("Equipes", "/api/saude/equipes", "Vínculos profissionais com vigência são auditados.");

    [HttpGet("/Saude/Agenda")]
    [HttpGet("/Saude/Agenda/Nova")]
    public async Task<IActionResult> Agenda(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        var res = await _agendaService.ListarAsync(new AgendaSaudeFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Agendas = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<AgendaSaudeResponse>();
        return View("Agenda", new AgendaSaudeFormViewModel());
    }

    [HttpPost("/Saude/Agenda/Nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgendaNovaPost([FromForm] AgendaSaudeCreateRequest request, CancellationToken ct)
    {
        var res = await _agendaService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao agendar.";
        else
            TempData["ToastOk"] = "Horário reservado.";
        return RedirectToAction(nameof(Agenda));
    }

    [HttpPost("/Saude/Agenda/Cancelar/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgendaCancelarPost(long id, [FromForm] string? motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["ToastErro"] = "Cancelar este horário? Informe o motivo.";
            return RedirectToAction(nameof(Agenda));
        }
        var res = await _agendaService.CancelarAsync(id, new CancelarAgendaRequest(motivo), ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao cancelar horário.";
        else
            TempData["ToastOk"] = "Horário cancelado.";
        return RedirectToAction(nameof(Agenda));
    }

    [HttpGet("/Saude/Acolhimentos")]
    public IActionResult Acolhimentos() => Operacao("Acolhimentos e triagem", "/api/saude/acolhimentos", "Queixa, sinais vitais e classificação de risco protegidos.");

    [HttpGet("/Saude/Acolhimentos/Novo")]
    public IActionResult AcolhimentoNovo() => Acolhimentos();

    [HttpGet("/Saude/Atendimentos")]
    [HttpGet("/Saude/Atendimentos/Novo")]
    public async Task<IActionResult> Atendimentos(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        var res = await _atendimentoService.ListarAsync(new AtendimentoSaudeFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Atendimentos = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<AtendimentoSaudeResponse>();
        return View("Atendimentos", new AtendimentoSaudeFormViewModel());
    }

    [HttpPost("/Saude/Atendimentos/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtendimentoNovoPost([FromForm] AtendimentoSaudeCreateRequest request, CancellationToken ct)
    {
        var res = await _atendimentoService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
        {
            TempData["ToastErro"] = res.Error ?? "Falha ao registrar acolhimento/atendimento.";
        }
        else
        {
            TempData["ToastOk"] = "Classificação de risco registrada.";
            if (string.Equals(request.ClassificacaoRisco, "VERMELHO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ClassificacaoRisco, "LARANJA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ToastAviso"] = "Risco alto registrado. Encaminhe ao atendimento. Conteúdo clínico não é exibido aqui.";
            }
        }
        return RedirectToAction(nameof(Atendimentos));
    }

    [HttpPost("/Saude/Atendimentos/Conduta/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtendimentoCondutaPost(long id, [FromForm] RegistrarCondutaRequest request, CancellationToken ct)
    {
        var res = await _atendimentoService.RegistrarCondutaAsync(id, request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao registrar conduta.";
        else
            TempData["ToastOk"] = "Evolução e conduta registradas.";
        return RedirectToAction(nameof(Atendimentos));
    }

    [HttpPost("/Saude/Atendimentos/Retificar/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtendimentoRetificarPost(long id, [FromForm] RetificarAtendimentoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Justificativa))
        {
            TempData["ToastErro"] = "Evolução finalizada. Use retificação justificada.";
            return RedirectToAction(nameof(Atendimentos));
        }
        var res = await _atendimentoService.RetificarAsync(id, request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao retificar evolução.";
        else
            TempData["ToastOk"] = "Retificação registrada com auditoria.";
        return RedirectToAction(nameof(Atendimentos));
    }

    [HttpGet("/Saude/Atendimentos/Detalhe/{id:long}")]
    public async Task<IActionResult> AtendimentoDetalhe(long id, CancellationToken ct)
    {
        ViewData["AtendimentoId"] = id;
        var res = await _atendimentoService.ObterAsync(id, ct).ConfigureAwait(false);
        ViewBag.Atendimento = res.IsSuccess ? res.Value : null;
        if (res.IsSuccess && res.Value is not null)
        {
            return await Prontuario(res.Value.PacienteId, ct).ConfigureAwait(false);
        }
        return View("Prontuario");
    }

    [HttpGet("/Saude/Prontuario/{pacienteId:long}")]
    public async Task<IActionResult> Prontuario(long pacienteId, CancellationToken ct)
    {
        ViewData["PacienteId"] = pacienteId;
        var pacRes = await _pacienteService.ObterAsync(pacienteId, ct).ConfigureAwait(false);
        ViewBag.Paciente = pacRes.IsSuccess ? pacRes.Value : null;
        var prtRes = await _prontuarioService.ObterPorPacienteAsync(pacienteId, ct).ConfigureAwait(false);
        ViewBag.Prontuario = prtRes.IsSuccess ? prtRes.Value : null;
        return View("Prontuario");
    }

    [HttpGet("/Saude/Vacinacao")]
    [HttpGet("/Saude/Vacinacao/Nova")]
    public async Task<IActionResult> Vacinacao(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        var res = await _vacinacaoService.ListarAsync(new PacienteFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Vacinacoes = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<VacinacaoResponse>();
        return View("Vacinacoes", new VacinacaoFormViewModel());
    }

    [HttpPost("/Saude/Vacinacao/Nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VacinacaoNovaPost([FromForm] VacinacaoCreateRequest request, CancellationToken ct)
    {
        var res = await _vacinacaoService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao registrar vacinação.";
        else
            TempData["ToastOk"] = "Dose de vacina registrada.";
        return RedirectToAction(nameof(Vacinacao));
    }

    [HttpGet("/Saude/Farmacia")]
    public async Task<IActionResult> Farmacia(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        var estRes = await _farmaciaService.ListarEstoqueAsync(new UnidadeSaudeFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Estoque = estRes.IsSuccess && estRes.Value is not null ? estRes.Value.Items : Array.Empty<FarmaciaEstoqueResponse>();
        return View("Farmacia", new FarmaciaProdutoFormViewModel());
    }

    [HttpGet("/Saude/Farmacia/Dispensar")]
    public async Task<IActionResult> Dispensar(CancellationToken ct) => await Farmacia(ct).ConfigureAwait(false);

    [HttpPost("/Saude/Farmacia/Dispensar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DispensarPost([FromForm] FarmaciaDispensacaoCreateRequest request, CancellationToken ct)
    {
        var res = await _farmaciaService.DispensarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
        {
            TempData["ToastErro"] = res.Error ?? "Falha na dispensação.";
        }
        else
        {
            TempData["ToastOk"] = "Dispensação realizada com sucesso.";
            TempData["ToastAviso"] = "Medicamento sem material de estoque. Não houve baixa.";
        }
        return RedirectToAction(nameof(Farmacia));
    }

    [HttpGet("/Saude/Regulacao")]
    [HttpGet("/Saude/Regulacao/Novo")]
    public async Task<IActionResult> Regulacao(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct).ConfigureAwait(false);
        var res = await _regulacaoService.ListarAsync(new PacienteFiltro(1, 50), ct).ConfigureAwait(false);
        ViewBag.Regulacoes = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<RegulacaoSolicitacaoResponse>();
        return View("Regulacao", new RegulacaoFormViewModel());
    }

    [HttpPost("/Saude/Regulacao/Nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegulacaoNovaPost([FromForm] RegulacaoSolicitacaoCreateRequest request, CancellationToken ct)
    {
        var res = await _regulacaoService.CriarAsync(request, ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao cadastrar regulação.";
        else
            TempData["ToastOk"] = "Solicitação de regulação registrada.";
        return RedirectToAction(nameof(Regulacao));
    }

    [HttpPost("/Saude/Regulacao/Status/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegulacaoStatusPost(long id, [FromForm] string status, [FromForm] string? justificativa, CancellationToken ct)
    {
        if (string.Equals(status, "DEVOLVIDA", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(justificativa))
        {
            TempData["ToastErro"] = "Devolução de regulação exige justificativa.";
            return RedirectToAction(nameof(Regulacao));
        }
        var res = await _regulacaoService.AlterarStatusAsync(id, new AlterarStatusRequest(status, justificativa), ct).ConfigureAwait(false);
        if (res.IsFailure)
            TempData["ToastErro"] = res.Error ?? "Falha ao alterar status da regulação.";
        else
            TempData["ToastOk"] = "Status da regulação atualizado.";
        return RedirectToAction(nameof(Regulacao));
    }

    private async Task CarregarOpcoesAsync(CancellationToken ct)
    {
        var uRes = await _unidadeService.ListarAsync(new UnidadeSaudeFiltro(1, 100, null, true), ct).ConfigureAwait(false);
        ViewBag.Unidades = uRes.IsSuccess && uRes.Value is not null ? uRes.Value.Items : Array.Empty<UnidadeSaudeResponse>();

        var pRes = await _profissionalService.ListarAsync(new ProfissionalSaudeFiltro(1, 100, null, "ATIVO"), ct).ConfigureAwait(false);
        ViewBag.Profissionais = pRes.IsSuccess && pRes.Value is not null ? pRes.Value.Items : Array.Empty<ProfissionalSaudeResponse>();

        var pacRes = await _pacienteService.ListarAsync(new PacienteFiltro(1, 100, null, "ATIVO"), ct).ConfigureAwait(false);
        ViewBag.Pacientes = pacRes.IsSuccess && pacRes.Value is not null ? pacRes.Value.Items : Array.Empty<PacienteResumoResponse>();

        var fRes = await _farmaciaService.ListarProdutosAsync(new UnidadeSaudeFiltro(1, 100), ct).ConfigureAwait(false);
        ViewBag.Produtos = fRes.IsSuccess && fRes.Value is not null ? fRes.Value.Items : Array.Empty<FarmaciaProdutoResponse>();
    }

    private async Task CarregarPessoasAsync(CancellationToken ct)
    {
        var res = await _pessoaService.ListarAsync(new PessoaFiltro(1, 100, TipoPessoa: "FISICA", Ativo: true), ct).ConfigureAwait(false);
        ViewBag.Pessoas = res.IsSuccess && res.Value is not null ? res.Value.Items : Array.Empty<PessoaResumoResponse>();
    }

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

    [HttpGet("/Saude/Unidades/Create")] public IActionResult UnidadeCreate() => Unidades(CancellationToken.None).GetAwaiter().GetResult();
    [HttpGet("/Saude/Unidades/Edit/{id:long}")] public IActionResult UnidadeEdit(long id) { ViewData["RegistroId"] = id; return Unidades(CancellationToken.None).GetAwaiter().GetResult(); }
    [HttpGet("/Saude/Unidades/Details/{id:long}")] public IActionResult UnidadeDetails(long id) { ViewData["RegistroId"] = id; return Unidades(CancellationToken.None).GetAwaiter().GetResult(); }
    [HttpGet("/Saude/Unidades/Equipes")] public IActionResult UnidadeEquipes() => Equipes();
    [HttpGet("/Saude/Unidades/Servicos")] public IActionResult UnidadeServicos() => Operacao("Serviços das unidades", "/api/saude/unidades/servicos", "Configure serviços somente para unidades ativas no contexto institucional selecionado.");
    [HttpGet("/Saude/Pacientes/Create")] public IActionResult PacienteCreate() => Pacientes(CancellationToken.None).GetAwaiter().GetResult();
    [HttpGet("/Saude/Pacientes/Edit/{id:long}")] public IActionResult PacienteEdit(long id) => PacienteDetalhe(id, CancellationToken.None).GetAwaiter().GetResult();
    [HttpGet("/Saude/Pacientes/Details/{id:long}")] public IActionResult PacienteDetails(long id) => PacienteDetalhe(id, CancellationToken.None).GetAwaiter().GetResult();
    [HttpGet("/Saude/Pacientes/Historico/{id:long}")] public IActionResult PacienteHistorico(long id) => PacienteDetalhe(id, CancellationToken.None).GetAwaiter().GetResult();
    [HttpGet("/Saude/Pacientes/Documentos/{id:long}")] public IActionResult PacienteDocumentos(long id) => PacienteDetalhe(id, CancellationToken.None).GetAwaiter().GetResult();
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
