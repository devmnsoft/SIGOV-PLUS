using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class LicitacoesController : Controller
{
    private readonly LicitacoesService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<LicitacoesController> _logger;
    public LicitacoesController(LicitacoesService service, IAuditTrailService auditTrail, ILogger<LicitacoesController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Licitacoes")]
    [Route("/Licitacoes/Processos")]
    [Route("/Licitacoes/Processos/Novo")]
    [Route("/Licitacoes/Itens")]
    [Route("/Licitacoes/Fornecedores")]
    [Route("/Licitacoes/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Licitacoes", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Licitacoes/Detalhes/{id:long}")]
    [HttpGet, Route("/Licitacoes/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Licitacoes/Processos/{id:long}")]
    [HttpGet, Route("/Licitacoes/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Licitacoes", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));

    [HttpPost, ValidateAntiForgeryToken, Route("/Licitacoes/Processos/Novo")]
    public async Task<IActionResult> Salvar(CancellationToken cancellationToken)
    {
        await Audit("licitacao.criar", "licitacao", null, cancellationToken).ConfigureAwait(false);
        TempData["Warning"] = "Registro não foi salvo: schema/regra oficial ainda não homologado. Nenhum número oficial foi gerado.";
        return Redirect("/Licitacoes");
    }

    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
