using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroPainelComercialController : Controller
{
    public IActionResult Index() => View("~/Views/Agro/PainelComercial.cshtml");
}
