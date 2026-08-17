using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
namespace Sigov.Web.Controllers;
[Authorize] public sealed class AtendimentoDigitalController:Controller { private readonly IAtendimentoDigitalService _service; public AtendimentoDigitalController(IAtendimentoDigitalService service)=>_service=service;
 [Route("/AtendimentoDigital")] public async Task<IActionResult> Index(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Index";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/Dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Dashboard";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/Chamados")] public async Task<IActionResult> Chamados(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Chamados";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/Detalhe")] public async Task<IActionResult> Detalhe(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Detalhe";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/ESic")] public async Task<IActionResult> ESic(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — ESic";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/Ouvidoria")] public async Task<IActionResult> Ouvidoria(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Ouvidoria";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/AtendimentoDigital/Satisfacao")] public async Task<IActionResult> Satisfacao(CancellationToken ct){var r=await _service.DashboardAsync("atendimento_digital_chamado",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="AtendimentoDigital — Satisfacao";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
}
