using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class QuickCreateController : Controller
{
    private readonly QuickCreateService _service;
    private readonly IAuditTrailService _audit;
    public QuickCreateController(QuickCreateService service, IAuditTrailService audit) { _service = service; _audit = audit; }

    [HttpGet("/QuickCreate/Iniciar")]
    public async Task<IActionResult> Iniciar(string? tipo, CancellationToken cancellationToken)
    {
        var option = string.IsNullOrWhiteSpace(tipo) ? null : _service.Find(tipo);
        if (option is null) return NotFound();
        if (!_service.CanStart(User, option))
        {
            TempData["Warning"] = "Seu perfil não permite iniciar este fluxo. Solicite acesso ao administrador do tenant.";
            return Redirect("/MinhaCentral");
        }

        await _audit.RegistrarAsync(null, null, "QUICK_CREATE_INICIADO", option.Key, null, null,
            new { tipo = option.Key, destino = option.Destination }, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        return LocalRedirect(option.Destination);
    }
}
