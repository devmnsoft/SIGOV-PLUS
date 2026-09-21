using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Educacao;
using Sigov.Web.Models.Educacao;

namespace Sigov.Web.Controllers;

/// <summary>
/// Entradas MVC do módulo Educação. As telas consomem exclusivamente os serviços/API
/// persistentes de Educação; recursos ainda em implantação possuem views próprias e
/// nunca recorrem ao catálogo demonstrativo operacional.
/// </summary>
public sealed class EducacaoController : Controller
{
    public IActionResult Importacoes() => View();
    [Route("/Educacao/Importacoes/{recurso}")]
    public IActionResult Importacao(string recurso) { ViewData["Recurso"] = recurso; return View("Importacao"); }
    private readonly IAlunoService _alunos;
    private readonly IEscolaService _escolas;
    private readonly IAnoLetivoService _anos;
    private readonly ITurmaService _turmas;
    private readonly IMatriculaService _matriculas;
    private readonly IProfessorService _professores;
    private readonly ICursoService _cursos;
    private readonly IPreMatriculaService _preMatriculas;
    private readonly IEducacaoDashboardService _dashboard;

    public EducacaoController(
        IAlunoService alunos,
        IEscolaService escolas,
        IAnoLetivoService anos,
        ITurmaService turmas,
        IMatriculaService matriculas,
        IProfessorService professores,
        ICursoService cursos,
        IPreMatriculaService preMatriculas,
        IEducacaoDashboardService dashboard)
    {
        _alunos = alunos;
        _escolas = escolas;
        _anos = anos;
        _turmas = turmas;
        _matriculas = matriculas;
        _professores = professores;
        _cursos = cursos;
        _preMatriculas = preMatriculas;
        _dashboard = dashboard;
    }

    private async Task CarregarOpcoesAsync(CancellationToken ct)
    {
        try
        {
            var escolasResult = await _escolas.ListarAsync(new EscolaFiltro(PageSize: 100, Ativo: true), ct).ConfigureAwait(false);
            ViewBag.Escolas = escolasResult.Value?.Items ?? Array.Empty<EscolaResponse>();

            var anosResult = await _anos.ListarAsync(new EscolaFiltro(PageSize: 50), ct).ConfigureAwait(false);
            ViewBag.AnosLetivos = anosResult.Value?.Items ?? Array.Empty<AnoLetivoResponse>();

            var turmasResult = await _turmas.ListarAsync(new TurmaFiltro(PageSize: 100), ct).ConfigureAwait(false);
            ViewBag.Turmas = turmasResult.Value?.Items ?? Array.Empty<TurmaResponse>();

            var alunosResult = await _alunos.ListarAsync(new AlunoFiltro(PageSize: 100), ct).ConfigureAwait(false);
            ViewBag.Alunos = alunosResult.Value?.Items ?? Array.Empty<AlunoResumoResponse>();

            var profResult = await _professores.ListarAsync(new EscolaFiltro(PageSize: 100), ct).ConfigureAwait(false);
            ViewBag.Professores = profResult.Value?.Items ?? Array.Empty<ProfessorResponse>();

            var cursosResult = await _cursos.ListarAsync(new EscolaFiltro(PageSize: 100), ct).ConfigureAwait(false);
            ViewBag.Cursos = cursosResult.Value?.Items ?? Array.Empty<CursoResponse>();
        }
        catch
        {
            ViewBag.Escolas ??= Array.Empty<EscolaResponse>();
            ViewBag.AnosLetivos ??= Array.Empty<AnoLetivoResponse>();
            ViewBag.Turmas ??= Array.Empty<TurmaResponse>();
            ViewBag.Alunos ??= Array.Empty<AlunoResumoResponse>();
            ViewBag.Professores ??= Array.Empty<ProfessorResponse>();
            ViewBag.Cursos ??= Array.Empty<CursoResponse>();
        }
    }

    [HttpGet("/Educacao")]
    [HttpGet("/Educacao/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct);
        var dash = await _dashboard.ObterAsync(ct).ConfigureAwait(false);
        if (dash.IsSuccess && dash.Value is not null)
        {
            ViewBag.DashboardReal = dash.Value;
        }
        return View(new EducacaoDashboardViewModel());
    }

    [HttpGet("/Educacao/Escolas")]
    public async Task<IActionResult> Escolas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new EscolaFormViewModel()); }

    [HttpGet("/Educacao/Escolas/Nova")]
    public async Task<IActionResult> EscolaNova(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Escolas", new EscolaFormViewModel()); }

    public async Task<IActionResult> EscolaDetalhe(long id, CancellationToken ct) { ViewData["EscolaId"] = id; await CarregarOpcoesAsync(ct); return View(new EscolaFormViewModel()); }
    public async Task<IActionResult> AnosLetivos(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new AnoLetivoFormViewModel()); }
    public async Task<IActionResult> Cursos(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new CursoFormViewModel()); }
    public async Task<IActionResult> Series(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new SerieAnoFormViewModel()); }

    [HttpGet("/Educacao/SeriesEtapas")]
    public async Task<IActionResult> SeriesEtapas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Series", new SerieAnoFormViewModel()); }

    [HttpGet("/Educacao/Turmas")]
    public async Task<IActionResult> Turmas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new TurmaFormViewModel()); }

    public async Task<IActionResult> TurmaDetalhe(long id, CancellationToken ct) { ViewData["TurmaId"] = id; await CarregarOpcoesAsync(ct); return View(); }

    [HttpGet("/Educacao/Turmas/Nova")]
    public async Task<IActionResult> TurmaNova(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Turmas", new TurmaFormViewModel()); }

    [HttpGet("/Educacao/Turmas/Detalhe/{id:long}")]
    public async Task<IActionResult> TurmaDetalheRota(long id, CancellationToken ct) => await TurmaDetalhe(id, ct);

    [HttpGet("/Educacao/Alunos")]
    public async Task<IActionResult> Alunos(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new AlunoFormViewModel()); }

    public async Task<IActionResult> AlunoCriar(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new AlunoFormViewModel()); }
    public async Task<IActionResult> AlunoEditar(long id, CancellationToken ct) { ViewData["AlunoId"] = id; await CarregarOpcoesAsync(ct); return View(new AlunoFormViewModel()); }
    public async Task<IActionResult> AlunoDetalhe(long id, CancellationToken ct) { ViewData["AlunoId"] = id; await CarregarOpcoesAsync(ct); return View(); }

    [HttpGet("/Educacao/Alunos/Detalhe/{id:long}")]
    public async Task<IActionResult> AlunoDetalheFunc05(long id, CancellationToken ct) => await AlunoDetalhe(id, ct);

    [HttpGet("/Educacao/Responsaveis")]
    public async Task<IActionResult> Responsaveis(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(); }

    [HttpGet("/Educacao/Matriculas")]
    public async Task<IActionResult> Matriculas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new MatriculaFormViewModel()); }

    public async Task<IActionResult> MatriculaDetalhe(long id, CancellationToken ct) { ViewData["MatriculaId"] = id; await CarregarOpcoesAsync(ct); return View(new MatriculaFormViewModel()); }
    public async Task<IActionResult> Professores(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new ProfessorFormViewModel()); }
    public async Task<IActionResult> ProfessorDetalhe(long id, CancellationToken ct) { ViewData["ProfessorId"] = id; await CarregarOpcoesAsync(ct); return View(new ProfessorFormViewModel()); }

    [HttpGet("/Educacao/Professores/Novo")]
    public async Task<IActionResult> ProfessorNovo(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Professores", new ProfessorFormViewModel()); }

    [HttpGet("/Educacao/Frequencia")]
    [HttpGet("/Educacao/Frequencias")]
    public async Task<IActionResult> Frequencias(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new FrequenciaFormViewModel()); }

    [HttpGet("/Educacao/Frequencias/Lancar")]
    public async Task<IActionResult> FrequenciaLancar(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Frequencias", new FrequenciaFormViewModel()); }

    public async Task<IActionResult> Avaliacoes(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new AvaliacaoFormViewModel()); }

    [HttpGet("/Educacao/Avaliacoes/LancarNotas")]
    public async Task<IActionResult> LancarNotas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Notas", new NotaFormViewModel()); }
    public async Task<IActionResult> Notas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new NotaFormViewModel()); }
    public async Task<IActionResult> PreMatriculas(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(new PreMatriculaFormViewModel()); }

    [HttpGet("/Educacao/PreMatriculas/Nova")]
    public async Task<IActionResult> PreMatriculaNova(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("PreMatriculas", new PreMatriculaFormViewModel()); }

    [HttpGet("/Educacao/Matriculas/Nova")]
    public async Task<IActionResult> MatriculaNova(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("Matriculas", new MatriculaFormViewModel()); }
    public async Task<IActionResult> PreMatriculaDetalhe(long id, CancellationToken ct) { ViewData["PreMatriculaId"] = id; await CarregarOpcoesAsync(ct); return View(new PreMatriculaFormViewModel()); }
    public IActionResult Educacenso() => View();
    public IActionResult Portal() => View();

    [HttpGet("/Educacao/{pagina:regex(^(Secretaria|DocumentosEscolares|DeclaracaoMatricula|DeclaracaoFrequencia|FichaCadastralAluno|HistoricoEscolar|SolicitacoesEscolares|PendenciasDocumentais|Transferencias|Ocorrencias|AtendimentoResponsavel|DiarioClasse|DiarioClasseDetalhe|DiarioAulas|DiarioFrequencia|DiarioConteudo|DiarioPendencias|PortalResponsavel|PortalAluno|PortalBoletim|PortalFrequencia|PortalOcorrencias|PortalSolicitacoes|PortalComunicados|PortalAdminVinculos|PortalAdminSolicitacoes)$)}")]
    public async Task<IActionResult> Bloco3(string pagina, CancellationToken ct)
    {
        await CarregarOpcoesAsync(ct);
        return View(pagina);
    }

    [HttpGet("/Educacao/Alunos/Novo")]
    public async Task<IActionResult> AlunoNovo(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View("AlunoCriar", new AlunoFormViewModel()); }

    [HttpPost("/Educacao/Alunos/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlunoNovoPost(AlunoFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await CarregarOpcoesAsync(ct);
            return View("AlunoCriar", model);
        }

        var result = await _alunos.CriarAsync(new AlunoCreateRequest(
            model.PessoaId,
            model.CodigoAluno,
            model.Nis,
            model.CartaoSus,
            model.NecessidadeEspecial,
            null,
            model.Situacao), ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Não foi possível cadastrar o aluno.");
            await CarregarOpcoesAsync(ct);
            return View("AlunoCriar", model);
        }

        TempData["Success"] = "Aluno cadastrado com persistência e auditoria.";
        return RedirectToAction(nameof(AlunoDetalhe), new { id = result.Value });
    }

    [HttpGet("/Educacao/Alunos/{id:long}")]
    public async Task<IActionResult> AlunoDetalheRota(long id, CancellationToken ct) => await AlunoDetalhe(id, ct);

    [HttpGet("/Educacao/Boletins")]
    public async Task<IActionResult> Boletins(CancellationToken ct) { await CarregarOpcoesAsync(ct); return View(); }

    [HttpGet("/Educacao/Transporte")]
    public IActionResult Transporte() => View();

    [HttpGet("/Educacao/Merenda")]
    public IActionResult Merenda() => View();

    [HttpGet("/Educacao/Biblioteca")]
    public IActionResult Biblioteca() => View();

    [HttpGet("/Educacao/Relatorios")]
    public IActionResult Relatorios() => View("RecursoOperacional", new EducacaoRecursoViewModel("Relatórios educacionais", "Exportações com escopo do tenant", "/api/educacao/export/alunos.csv"));
}
