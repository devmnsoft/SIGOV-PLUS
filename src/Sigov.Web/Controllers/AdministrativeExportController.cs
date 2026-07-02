using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class AdministrativeExportController : Controller
{
    private static readonly HashSet<string> AllowedModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "Siafic", "Planejamento", "Tesouraria", "Compras", "Licitacoes", "Contratos", "Almoxarifado", "Patrimonio", "Frotas", "Obras", "Transparencia"
    };

    private readonly OperationalDemoService _fallback;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<AdministrativeExportController> _logger;

    public AdministrativeExportController(OperationalDemoService fallback, IAuditTrailService auditTrail, ILogger<AdministrativeExportController> logger)
    {
        _fallback = fallback;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    [HttpGet("/{module}/ExportCsv")]
    public async Task<IActionResult> ExportCsv(string module, CancellationToken cancellationToken)
    {
        if (!AllowedModules.Contains(module)) return NotFound();
        try
        {
            var rows = _fallback.Build(module).Records;
            try { await _auditTrail.RegistrarAsync(null, null, $"{module.ToLowerInvariant()}.exportar_csv", module, null, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false); } catch (Exception auditEx) { _logger.LogWarning(auditEx, "Auditoria de exportação administrativa em fallback"); }
            var csv = "Id;Codigo;Nome;Status;Responsavel;AtualizadoEm\n" + string.Join("\n", rows.Select(r => $"{r.Id};{Safe(r.Codigo)};{Safe(r.Nome)};{Safe(r.Status)};{Safe(r.Responsavel)};{Safe(r.AtualizadoEm)}"));
            return File(new UTF8Encoding(true).GetBytes(csv), "text/csv; charset=utf-8", $"{module.ToLowerInvariant()}-fallback-honesto.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao exportar CSV administrativo {Module}", module);
            return File(new UTF8Encoding(true).GetBytes("mensagem\nExportação indisponível no momento.\n"), "text/csv; charset=utf-8", $"{module.ToLowerInvariant()}-erro.csv");
        }
    }

    private static string Safe(string value) => value.Replace(";", ",", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
