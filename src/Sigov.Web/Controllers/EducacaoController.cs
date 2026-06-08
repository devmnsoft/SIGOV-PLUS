using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Educacao;

namespace Sigov.Web.Controllers;

public sealed class EducacaoController : Controller
{
    public IActionResult Dashboard() => View(new EducacaoDashboardViewModel());
    public IActionResult Escolas() => View(new EscolaFormViewModel());
    public IActionResult EscolaDetalhe(long id) { ViewData["EscolaId"] = id; return View(new EscolaFormViewModel()); }
    public IActionResult AnosLetivos() => View(new AnoLetivoFormViewModel());
    public IActionResult Cursos() => View(new CursoFormViewModel());
    public IActionResult Series() => View(new SerieAnoFormViewModel());
    public IActionResult Turmas() => View(new TurmaFormViewModel());
    public IActionResult TurmaDetalhe(long id) { ViewData["TurmaId"] = id; return View(); }
    public IActionResult Alunos() => View(new AlunoFormViewModel());
    public IActionResult AlunoCriar() => View(new AlunoFormViewModel());
    public IActionResult AlunoEditar(long id) { ViewData["AlunoId"] = id; return View(new AlunoFormViewModel()); }
    public IActionResult AlunoDetalhe(long id) { ViewData["AlunoId"] = id; return View(); }
    public IActionResult Matriculas() => View(new MatriculaFormViewModel());
    public IActionResult MatriculaDetalhe(long id) { ViewData["MatriculaId"] = id; return View(new MatriculaFormViewModel()); }
    public IActionResult Professores() => View(new ProfessorFormViewModel());
    public IActionResult ProfessorDetalhe(long id) { ViewData["ProfessorId"] = id; return View(new ProfessorFormViewModel()); }
    public IActionResult Frequencias() => View(new FrequenciaFormViewModel());
    public IActionResult Avaliacoes() => View(new AvaliacaoFormViewModel());
    public IActionResult Notas() => View(new NotaFormViewModel());
    public IActionResult PreMatriculas() => View(new PreMatriculaFormViewModel());
    public IActionResult PreMatriculaDetalhe(long id) { ViewData["PreMatriculaId"] = id; return View(new PreMatriculaFormViewModel()); }
    public IActionResult Educacenso() => View();
    public IActionResult Portal() => View();
}
