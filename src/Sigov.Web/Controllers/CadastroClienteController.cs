using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class CadastroClienteController : Controller
{
    public IActionResult Index(string? plano) { ViewData["Plano"] = plano; return View(); }
    public IActionResult Confirmacao(string? protocolo) { ViewData["Protocolo"] = protocolo; return View(); }
}
