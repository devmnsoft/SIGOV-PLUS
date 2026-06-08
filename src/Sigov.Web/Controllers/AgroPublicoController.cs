using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class AgroPublicoController : Controller
{
    public IActionResult Painel(string tenantSlug) { ViewData["TenantSlug"] = tenantSlug; return View("~/Views/AgroPublico/Painel.cshtml"); }
    public IActionResult Datasets(string tenantSlug) { ViewData["TenantSlug"] = tenantSlug; return View("~/Views/AgroPublico/Datasets.cshtml"); }
}
