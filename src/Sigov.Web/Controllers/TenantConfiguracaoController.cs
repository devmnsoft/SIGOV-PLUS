using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class TenantConfiguracaoController : Controller
{
    public IActionResult MinhaAssinatura() => View();
    public IActionResult MeusModulos() => View();
    public IActionResult Branding() => View();
    public IActionResult Dominios() => View();
}
