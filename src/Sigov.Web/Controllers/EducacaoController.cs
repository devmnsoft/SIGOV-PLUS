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

    public EducacaoController(IAlunoService alunos) => _alunos = alunos;

    [HttpGet("/Educacao")]
    [HttpGet("/Educacao/Dashboard")]
    public IActionResult Dashboard() => View(new EducacaoDashboardViewModel());

    [HttpGet("/Educacao/Escolas")]
    public IActionResult Escolas() => View(new EscolaFormViewModel());

    [HttpGet("/Educacao/Escolas/Nova")]
    public IActionResult EscolaNova() => View("Escolas", new EscolaFormViewModel());

    public IActionResult EscolaDetalhe(long id) { ViewData["EscolaId"] = id; return View(new EscolaFormViewModel()); }
    public IActionResult AnosLetivos() => View(new AnoLetivoFormViewModel());
    public IActionResult Cursos() => View(new CursoFormViewModel());
    public IActionResult Series() => View(new SerieAnoFormViewModel());

    [HttpGet("/Educacao/SeriesEtapas")]
    public IActionResult SeriesEtapas() => View("Series", new SerieAnoFormViewModel());

    [HttpGet("/Educacao/Turmas")]
    public IActionResult Turmas() => View(new TurmaFormViewModel());

    public IActionResult TurmaDetalhe(long id) { ViewData["TurmaId"] = id; return View(); }

    [HttpGet("/Educacao/Turmas/Nova")]
    public IActionResult TurmaNova() => View("Turmas", new TurmaFormViewModel());

    [HttpGet("/Educacao/Turmas/Detalhe/{id:long}")]
    public IActionResult TurmaDetalheRota(long id) => TurmaDetalhe(id);

    [HttpGet("/Educacao/Alunos")]
    public IActionResult Alunos() => View(new AlunoFormViewModel());

    public IActionResult AlunoCriar() => View(new AlunoFormViewModel());
    public IActionResult AlunoEditar(long id) { ViewData["AlunoId"] = id; return View(new AlunoFormViewModel()); }
    public IActionResult AlunoDetalhe(long id) { ViewData["AlunoId"] = id; return View(); }

    [HttpGet("/Educacao/Alunos/Detalhe/{id:long}")]
    public IActionResult AlunoDetalheFunc05(long id) => AlunoDetalhe(id);

    [HttpGet("/Educacao/Responsaveis")]
    public IActionResult Responsaveis() => View();

    [HttpGet("/Educacao/Matriculas")]
    public IActionResult Matriculas() => View(new MatriculaFormViewModel());

    public IActionResult MatriculaDetalhe(long id) { ViewData["MatriculaId"] = id; return View(new MatriculaFormViewModel()); }
    public IActionResult Professores() => View(new ProfessorFormViewModel());
    public IActionResult ProfessorDetalhe(long id) { ViewData["ProfessorId"] = id; return View(new ProfessorFormViewModel()); }

    [HttpGet("/Educacao/Professores/Novo")]
    public IActionResult ProfessorNovo() => View("Professores", new ProfessorFormViewModel());

    [HttpGet("/Educacao/Frequencia")]
    [HttpGet("/Educacao/Frequencias")]
    public IActionResult Frequencias() => View(new FrequenciaFormViewModel());

    [HttpGet("/Educacao/Frequencias/Lancar")]
    public IActionResult FrequenciaLancar() => View("Frequencias", new FrequenciaFormViewModel());

    public IActionResult Avaliacoes() => View(new AvaliacaoFormViewModel());

    [HttpGet("/Educacao/Avaliacoes/LancarNotas")]
    public IActionResult LancarNotas() => View("Notas", new NotaFormViewModel());
    public IActionResult Notas() => View(new NotaFormViewModel());
    public IActionResult PreMatriculas() => View(new PreMatriculaFormViewModel());

    [HttpGet("/Educacao/PreMatriculas/Nova")]
    public IActionResult PreMatriculaNova() => View("PreMatriculas", new PreMatriculaFormViewModel());

    [HttpGet("/Educacao/Matriculas/Nova")]
    public IActionResult MatriculaNova() => View("Matriculas", new MatriculaFormViewModel());
    public IActionResult PreMatriculaDetalhe(long id) { ViewData["PreMatriculaId"] = id; return View(new PreMatriculaFormViewModel()); }
    public IActionResult Educacenso() => View();
    public IActionResult Portal() => View();

    [HttpGet("/Educacao/{pagina:regex(^(Secretaria|DocumentosEscolares|DeclaracaoMatricula|DeclaracaoFrequencia|FichaCadastralAluno|HistoricoEscolar|SolicitacoesEscolares|PendenciasDocumentais|Transferencias|Ocorrencias|AtendimentoResponsavel|DiarioClasse|DiarioClasseDetalhe|DiarioAulas|DiarioFrequencia|DiarioConteudo|DiarioPendencias|PortalResponsavel|PortalAluno|PortalBoletim|PortalFrequencia|PortalOcorrencias|PortalSolicitacoes|PortalComunicados|PortalAdminVinculos|PortalAdminSolicitacoes)$)}")]
    public IActionResult Bloco3(string pagina) => View(pagina);

    [HttpGet("/Educacao/Alunos/Novo")]
    public IActionResult AlunoNovo() => View("AlunoCriar", new AlunoFormViewModel());

    [HttpPost("/Educacao/Alunos/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlunoNovoPost(AlunoFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("AlunoCriar", model);

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
            return View("AlunoCriar", model);
        }

        TempData["Success"] = "Aluno cadastrado com persistência e auditoria.";
        return RedirectToAction(nameof(AlunoDetalhe), new { id = result.Value });
    }

    [HttpGet("/Educacao/Alunos/{id:long}")]
    public IActionResult AlunoDetalheRota(long id) => AlunoDetalhe(id);

    [HttpGet("/Educacao/Boletins")]
    public IActionResult Boletins() => View();

    [HttpGet("/Educacao/Transporte")]
    public IActionResult Transporte() => View();

    [HttpGet("/Educacao/Merenda")]
    public IActionResult Merenda() => View();

    [HttpGet("/Educacao/Biblioteca")]
    public IActionResult Biblioteca() => View();

    [HttpGet("/Educacao/Relatorios")]
    public IActionResult Relatorios() => View("RecursoOperacional", new EducacaoRecursoViewModel("Relatórios educacionais", "Exportações com escopo do tenant", "/api/educacao/export/alunos.csv"));
}
