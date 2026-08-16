using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class PatrimonioController : Controller
{
    private readonly PatrimonioService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<PatrimonioController> _logger;
    public PatrimonioController(PatrimonioService service, IAuditTrailService auditTrail, ILogger<PatrimonioController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Patrimonio")]
    [Route("/Patrimonio/Bens")]
    [Route("/Patrimonio/Dashboard")]
    [Route("/Patrimonio/Bens/Novo")]
    [Route("/Patrimonio/Movimentos")]
    [Route("/Patrimonio/Localizacoes")]
    [Route("/Patrimonio/Responsaveis")]
    [Route("/Patrimonio/Inventario")]
    [Route("/Patrimonio/Inventarios")]
    [Route("/Patrimonio/Depreciacao")]
    [Route("/Patrimonio/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Patrimonio", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Patrimonio/Detalhes/{id:long}")]
    [HttpGet, Route("/Patrimonio/Bens/{id:long}/Editar")]
    [HttpGet, Route("/Patrimonio/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Patrimonio/Processos/{id:long}")]
    [HttpGet, Route("/Patrimonio/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Patrimonio", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));

    [HttpGet, Route("/Patrimonio/BensCsv")]
    public IActionResult BensCsv() => File(System.Text.Encoding.UTF8.GetBytes("mensagem\nExportação em implantação neste ambiente; schema patrimonio_bem não confirmado.\n"), "text/csv; charset=utf-8", "patrimonio-bens.csv");

    [HttpPost, Route("/Patrimonio/Bens/Novo")]
    [HttpPost, Route("/Patrimonio/Bens/{id:long}/Editar")]
    [HttpPost, Route("/Patrimonio/Bens/{id:long}/Baixar")]
    public async Task<IActionResult> Salvar(CancellationToken cancellationToken)
    {
        await Audit("patrimonio_bem.criar", "patrimonio_bem", null, cancellationToken).ConfigureAwait(false);
        TempData["Warning"] = "Registro não foi salvo: schema/regra oficial ainda não homologado. Nenhum número oficial foi gerado.";
        return Redirect("/Patrimonio");
    }

    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
