using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ComprasController : Controller
{
    private readonly ComprasService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<ComprasController> _logger;
    public ComprasController(ComprasService service, IAuditTrailService auditTrail, ILogger<ComprasController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Compras")]
    [Route("/Compras/Dashboard")]
    [Route("/Compras/Solicitacoes")]
    [Route("/Compras/Solicitacoes/Nova")]
    [Route("/Compras/Fornecedores")]
    [Route("/Compras/Itens")]
    [Route("/Compras/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Compras", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Compras/Detalhes/{id:long}")]
    [HttpGet, Route("/Compras/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Compras/Processos/{id:long}")]
    [HttpGet, Route("/Compras/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Compras", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));

    [HttpPost, ValidateAntiForgeryToken, Route("/Compras/Solicitacoes/Nova")]
    public async Task<IActionResult> Salvar(CancellationToken cancellationToken)
    {
        await Audit("compra_solicitacao.criar", "compra_solicitacao", null, cancellationToken).ConfigureAwait(false);
        TempData["Warning"] = "Registro não foi salvo: schema/regra oficial ainda não homologado. Nenhum número oficial foi gerado.";
        return Redirect("/Compras");
    }

    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
