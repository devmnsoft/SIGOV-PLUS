using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class CoreController : Controller
{
    public IActionResult Entidades() => View();
    public IActionResult Exercicios() => View();
    public IActionResult Unidades() => View();
}
