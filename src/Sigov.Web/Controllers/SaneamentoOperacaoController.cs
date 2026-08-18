using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize, Route("Saneamento/Operacao/{action=Dashboard}")] public sealed class SaneamentoOperacaoController : Controller
{
 public IActionResult Dashboard() => Tela("Dashboard", "operacao", "dashboard");
 public IActionResult Equipes() => Tela("Equipes", "operacao", "equipes");
 public IActionResult Ordens() => Tela("Ordens", "operacao", "ordens");
 public IActionResult OrdemDetalhe() => Tela("OrdemDetalhe", "operacao", "ordens");
 public IActionResult Cortes() => Tela("Cortes", "operacao", "cortes");
 public IActionResult Religacoes() => Tela("Religacoes", "operacao", "religacoes");
 public IActionResult Vazamentos() => Tela("Vazamentos", "operacao", "vazamentos");
 public IActionResult Vistorias() => Tela("Vistorias", "operacao", "vistorias");
 public IActionResult Relatorios() => Tela("Relatorios", "operacao", "relatorios");
 private IActionResult Tela(string titulo,string modulo,string recurso){ViewData["Titulo"]=titulo;ViewData["Modulo"]=modulo;ViewData["Recurso"]=recurso;return View("~/Views/Saneamento/Avancado.cshtml");}
}
