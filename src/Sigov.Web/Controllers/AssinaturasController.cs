using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
namespace Sigov.Web.Controllers;
[Authorize] public sealed class AssinaturasController:Controller { private readonly IAssinaturaService _service; public AssinaturasController(IAssinaturaService service)=>_service=service;
 [Route("/Assinaturas")] public async Task<IActionResult> Index(CancellationToken ct){var r=await _service.DashboardAsync("assinatura_documento",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="Assinaturas — Index";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/Assinaturas/Dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct){var r=await _service.DashboardAsync("assinatura_documento",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="Assinaturas — Dashboard";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/Assinaturas/Detalhe")] public async Task<IActionResult> Detalhe(CancellationToken ct){var r=await _service.DashboardAsync("assinatura_documento",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="Assinaturas — Detalhe";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/Assinaturas/Pendentes")] public async Task<IActionResult> Pendentes(CancellationToken ct){var r=await _service.DashboardAsync("assinatura_documento",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="Assinaturas — Pendentes";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/Assinaturas/Validar")] public async Task<IActionResult> Validar(CancellationToken ct){var r=await _service.DashboardAsync("assinatura_documento",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="Assinaturas — Validar";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
}
