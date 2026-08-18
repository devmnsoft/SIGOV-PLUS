using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize, Route("Saneamento/Comercial/{action=Dashboard}")] public sealed class SaneamentoComercialController : Controller
{
 public IActionResult Dashboard() => Tela("Dashboard", "comercial", "dashboard");
 public IActionResult Consumidores() => Tela("Consumidores", "comercial", "consumidores");
 public IActionResult ConsumidorDetalhe() => Tela("ConsumidorDetalhe", "comercial", "consumidores");
 public IActionResult Ligacoes() => Tela("Ligacoes", "comercial", "ligacoes");
 public IActionResult LigacaoDetalhe() => Tela("LigacaoDetalhe", "comercial", "ligacoes");
 public IActionResult Hidrometros() => Tela("Hidrometros", "comercial", "hidrometros");
 public IActionResult Tarifas() => Tela("Tarifas", "comercial", "tarifas");
 public IActionResult Atendimentos() => Tela("Atendimentos", "comercial", "atendimentos");
 public IActionResult Relatorios() => Tela("Relatorios", "comercial", "relatorios");
 private IActionResult Tela(string titulo,string modulo,string recurso){ViewData["Titulo"]=titulo;ViewData["Modulo"]=modulo;ViewData["Recurso"]=recurso;return View("~/Views/Saneamento/Avancado.cshtml");}
}
