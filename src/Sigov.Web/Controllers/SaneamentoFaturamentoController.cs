using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize, Route("Saneamento/Faturamento/{action=Dashboard}")] public sealed class SaneamentoFaturamentoController : Controller
{
 public IActionResult Dashboard() => Tela("Dashboard", "faturamento", "dashboard");
 public IActionResult RotasLeitura() => Tela("RotasLeitura", "faturamento", "rotas-leitura");
 public IActionResult Leituras() => Tela("Leituras", "faturamento", "leituras");
 public IActionResult Lotes() => Tela("Lotes", "faturamento", "lotes");
 public IActionResult Faturas() => Tela("Faturas", "faturamento", "faturas");
 public IActionResult Pagamentos() => Tela("Pagamentos", "faturamento", "pagamentos");
 public IActionResult Inadimplencia() => Tela("Inadimplencia", "faturamento", "inadimplencia");
 public IActionResult Parcelamentos() => Tela("Parcelamentos", "faturamento", "parcelamentos");
 public IActionResult Relatorios() => Tela("Relatorios", "faturamento", "relatorios");
 private IActionResult Tela(string titulo,string modulo,string recurso){ViewData["Titulo"]=titulo;ViewData["Modulo"]=modulo;ViewData["Recurso"]=recurso;return View("~/Views/Saneamento/Avancado.cshtml");}
}
