using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Social;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SocialController : Controller
{
    private readonly SectorModuleService _sector;
    public SocialController(SectorModuleService sector) => _sector = sector;

    [HttpGet("/Social")]
    [HttpGet("/AssistenciaSocial")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken) =>
        View("~/Views/Sectors/Module.cshtml", await _sector.BuildAsync("AssistenciaSocial", "Assistência Social360", "Proteção social multi-esfera, SUAS, famílias, benefícios, visitas e acolhimento com LGPD reforçada.", new[] { "Famílias", "Atendimentos", "Benefícios", "Visitas", "Acolhimentos" }, new[] { "/AssistenciaSocial/Familias", "/AssistenciaSocial/Atendimentos", "/AssistenciaSocial/Beneficios", "/AssistenciaSocial/Visitas", "/AssistenciaSocial/Acolhimentos" }, true, q, cancellationToken));

    [Authorize(Policy = "ASSISTENCIA_DASHBOARD_VIEW")]
    [HttpGet("/Social/Dashboard")]
    [HttpGet("/AssistenciaSocial/Dashboard")]
    public IActionResult Dashboard() => View(new SocialDashboardViewModel());

    [HttpGet("/AssistenciaSocial/{area:regex(^(Unidades|CRAS|CREAS|Servicos|Equipes|Familias|Pessoas|Atendimentos|Acompanhamentos|PlanosAcompanhamento|Encaminhamentos|Vulnerabilidades|Riscos|ViolacoesDireitos|Beneficios|Programas|Visitas|Agenda|Acolhimentos|ConselhoTutelar|MedidasProtecao|RedeProtecao|Relatorios)$)}")]
    [HttpGet("/AssistenciaSocial/{area:regex(^(Familias|Pessoas|Atendimentos|Visitas|Acolhimentos)$)}/{operation:regex(^(Create|Edit|Details|Movimentacoes)$)}")]
    [HttpGet("/AssistenciaSocial/{area:regex(^(Beneficios|Programas)$)}/{operation:regex(^(Solicitacoes|Concessoes|Prestacao|Inscricoes)$)}")]
    public IActionResult Area(string area, string? operation = null)
    {
        ViewData["Area"] = area;
        ViewData["Operation"] = operation;
        return View("Area");
    }
}
