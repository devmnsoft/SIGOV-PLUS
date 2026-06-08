using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class SaasComercialController : Controller
{
    public IActionResult Planos() => View();
    public IActionResult Solicitacoes() => View();
    public IActionResult SolicitacaoDetalhe(long id) { ViewData["SolicitacaoId"] = id; return View(); }
    public IActionResult Assinaturas() => View();
    public IActionResult PerfilTemplates() => View();
}
