using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
[Authorize, Route("Saneamento/GisQualidade/{action=Dashboard}")] public sealed class SaneamentoGisQualidadeController : Controller
{
 public IActionResult Dashboard() => Tela("Dashboard", "gis-qualidade", "dashboard");
 public IActionResult Mapa() => Tela("Mapa", "gis-qualidade", "pontos");
 public IActionResult UnidadesOperacionais() => Tela("UnidadesOperacionais", "gis-qualidade", "unidades-operacionais");
 public IActionResult Pontos() => Tela("Pontos", "gis-qualidade", "pontos");
 public IActionResult Redes() => Tela("Redes", "gis-qualidade", "redes");
 public IActionResult Laboratorio() => Tela("Laboratorio", "gis-qualidade", "amostras");
 public IActionResult Amostras() => Tela("Amostras", "gis-qualidade", "amostras");
 public IActionResult Ensaios() => Tela("Ensaios", "gis-qualidade", "amostras");
 public IActionResult Alertas() => Tela("Alertas", "gis-qualidade", "alertas");
 public IActionResult Relatorios() => Tela("Relatorios", "gis-qualidade", "relatorios");
 private IActionResult Tela(string titulo,string modulo,string recurso){ViewData["Titulo"]=titulo;ViewData["Modulo"]=modulo;ViewData["Recurso"]=recurso;return View("~/Views/Saneamento/Avancado.cshtml");}
}
