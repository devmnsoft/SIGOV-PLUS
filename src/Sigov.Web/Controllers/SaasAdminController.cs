using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class SaasAdminController : Controller
{
    public IActionResult Tenants() => View();
    public IActionResult TenantDetalhe() => View();
    public IActionResult NovoTenant() => View();
    public IActionResult Planos() => View();
    public IActionResult Modulos() => View();
    public IActionResult Assinaturas() => View();
    public IActionResult FeatureFlags() => View();
    public IActionResult Uso() => View();
    public IActionResult Operacao() => View();
}
