using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
namespace Sigov.Web.Controllers;
[Authorize] public sealed class ProcessosDigitaisController:Controller { private readonly IProcessosDigitaisService _service; public ProcessosDigitaisController(IProcessosDigitaisService service)=>_service=service;
 [Route("/ProcessosDigitais")] public async Task<IActionResult> Index(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Index";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Dashboard";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Novo")] public async Task<IActionResult> Novo(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Novo";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Detalhe")] public async Task<IActionResult> Detalhe(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Detalhe";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Movimentacoes")] public async Task<IActionResult> Movimentacoes(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Movimentacoes";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Prazos")] public async Task<IActionResult> Prazos(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Prazos";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProcessosDigitais/Relatorios")] public async Task<IActionResult> Relatorios(CancellationToken ct){var r=await _service.DashboardAsync("processo_digital",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProcessosDigitais — Relatorios";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
}
