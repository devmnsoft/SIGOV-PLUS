using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class SuporteController : Controller
{
    public IActionResult Chamados() => View();

    public IActionResult NovoChamado() => View();
}
