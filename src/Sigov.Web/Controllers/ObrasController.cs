using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ObrasController : Controller
{
    private readonly ObrasService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<ObrasController> _logger;
    public ObrasController(ObrasService service, IAuditTrailService auditTrail, ILogger<ObrasController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Obras")]
    [Route("/Obras/Listar")]
    [Route("/Obras/Nova")]
    [Route("/Obras/Medicoes")]
    [Route("/Obras/Diario")]
    [Route("/Obras/Fotos")]
    [Route("/Obras/Fiscalizacao")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Obras", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Obras/Detalhes/{id:long}")]
    [HttpGet, Route("/Obras/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Obras/Processos/{id:long}")]
    [HttpGet, Route("/Obras/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Obras", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));

    [HttpPost, ValidateAntiForgeryToken, Route("/Obras/Nova")]
    public async Task<IActionResult> Salvar(CancellationToken cancellationToken)
    {
        await Audit("obra.criar", "obra", null, cancellationToken).ConfigureAwait(false);
        TempData["Warning"] = "Registro não foi salvo: schema/regra oficial ainda não homologado. Nenhum número oficial foi gerado.";
        return Redirect("/Obras");
    }

    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
