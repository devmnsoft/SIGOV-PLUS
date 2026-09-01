using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize(Policy = "PORTAL_CIDADAO_ASSISTENCIA_ACCESS")]
public sealed class PortalCidadaoAssistenciaController : Controller
{
    [HttpGet("/PortalCidadao/AssistenciaSocial")]
    [HttpGet("/PortalCidadao/AssistenciaSocial/{area:regex(^(Solicitacoes|Beneficios|Agendamentos|Encaminhamentos)$)}")]
    public IActionResult Index(string? area = null)
    {
        ViewData["Area"] = area ?? "Resumo";
        return View();
    }
}
