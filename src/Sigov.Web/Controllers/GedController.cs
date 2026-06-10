using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class GedController : Controller
{
    public IActionResult Dashboard() => View();
    public IActionResult Documentos() => View();
    public IActionResult Upload() => View();
    public IActionResult Pesquisa() => View();
    public IActionResult Workflow() => View();
    public IActionResult Historico() => View();
    public IActionResult AssinaturaTeste() => View();
    public IActionResult Contratos() => View();
    public IActionResult Tramitacoes() => View();
    public IActionResult Ocr() => View();
}
