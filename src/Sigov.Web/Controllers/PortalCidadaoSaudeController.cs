using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize(Policy = "PORTAL_CIDADAO_SAUDE_ACCESS")]
public sealed class PortalCidadaoSaudeController : Controller
{
    [HttpGet("/PortalCidadao/Saude")]
    [HttpGet("/PortalCidadao/Saude/{area:regex(^(Agendamentos|Solicitacoes|Medicamentos|Vacinacao|MinhaFila)$)}")]
    public IActionResult Index(string? area = null)
    {
        ViewData["Area"] = area ?? "Resumo";
        return View();
    }
}
