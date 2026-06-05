using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Educacao;

namespace Sigov.Web.Controllers;

public sealed class EducacaoController : Controller
{
    public IActionResult Dashboard() => View(new EducacaoDashboardViewModel());
    public IActionResult Escolas() => View(new EscolaFormViewModel());
    public IActionResult AnosLetivos() => View();
    public IActionResult Cursos() => View();
    public IActionResult Turmas() => View(new TurmaFormViewModel());
    public IActionResult TurmaDetalhe(long id) { ViewData["TurmaId"] = id; return View(); }
    public IActionResult Alunos() => View(new AlunoFormViewModel());
    public IActionResult AlunoCriar() => View(new AlunoFormViewModel());
    public IActionResult AlunoDetalhe(long id) { ViewData["AlunoId"] = id; return View(); }
    public IActionResult Matriculas() => View(new MatriculaFormViewModel());
    public IActionResult Professores() => View(new ProfessorFormViewModel());
    public IActionResult Frequencias() => View(new FrequenciaFormViewModel());
    public IActionResult Avaliacoes() => View(new AvaliacaoFormViewModel());
    public IActionResult PreMatriculas() => View(new PreMatriculaFormViewModel());
    public IActionResult Educacenso() => View();
    public IActionResult Portal() => View();
}
