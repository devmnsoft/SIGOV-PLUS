using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class OperacaoController : Controller
{
    public IActionResult Health() => View();
}
