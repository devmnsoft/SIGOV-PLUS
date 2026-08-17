using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Bloco8;
namespace Sigov.Web.Controllers;
[Authorize] public sealed class ProtocoloExternoController:Controller { private readonly IProtocoloDigitalService _service; public ProtocoloExternoController(IProtocoloDigitalService service)=>_service=service;
 [Route("/ProtocoloExterno")] public async Task<IActionResult> Index(CancellationToken ct){var r=await _service.DashboardAsync("protocolo_externo",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProtocoloExterno — Index";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProtocoloExterno/Novo")] public async Task<IActionResult> Novo(CancellationToken ct){var r=await _service.DashboardAsync("protocolo_externo",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProtocoloExterno — Novo";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
 [Route("/ProtocoloExterno/Consulta")] public async Task<IActionResult> Consulta(CancellationToken ct){var r=await _service.DashboardAsync("protocolo_externo",ct);if(r.IsFailure)return Problem(r.Error);ViewData["Module"]="ProtocoloExterno — Consulta";return View("~/Views/Bloco8/Module.cshtml",r.Value);}
}
