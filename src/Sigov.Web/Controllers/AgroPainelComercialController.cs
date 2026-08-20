using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class AgroPainelComercialController : Controller
{
    public IActionResult Index() => View("~/Views/Agro/PainelComercial.cshtml");
}
