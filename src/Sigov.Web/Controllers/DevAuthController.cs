using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services.Development;

namespace Sigov.Web.Controllers;

[AllowAnonymous]
[Route("Dev/Auth")]
public sealed class DevAuthController : Controller
{
    private readonly DevelopmentAuthDiagnosticService _diagnostics;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public DevAuthController(DevelopmentAuthDiagnosticService diagnostics, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _diagnostics = diagnostics; _environment = environment; _configuration = configuration;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!CanAccess()) return NotFound();
        return View(await _diagnostics.DiagnoseAsync(cancellationToken: ct));
    }

    [HttpGet("Status")]
    public async Task<IActionResult> Status(CancellationToken ct) =>
        CanAccess() ? Json(await _diagnostics.DiagnoseAsync(cancellationToken: ct)) : NotFound();

    [HttpPost("ResetAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAdmin(CancellationToken ct)
    {
        if (!CanAccess()) return NotFound();
        var report = await _diagnostics.ResetAdminAsync(HttpContext.TraceIdentifier,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), ct);
        TempData["DevAuthMessage"] = report.FinalReason == "OK" ? "Acessos administrativos locais reparados e validados com sucesso." : $"Reset concluído; diagnóstico: {report.FinalReason}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("TestLogin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestLogin(CancellationToken ct)
    {
        if (!CanAccess()) return NotFound();
        var report = await _diagnostics.DiagnoseAsync(cancellationToken: ct);
        TempData["DevAuthMessage"] = $"Teste de login (sem cookie): {report.FinalReason}.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanAccess()
    {
        if (!_environment.IsDevelopment()) return false;
        var remote = HttpContext.Connection.RemoteIpAddress;
        if (remote is not null && IPAddress.IsLoopback(remote)) return true;
        var expected = _configuration["Sigov:DevBootstrapToken"] ?? Environment.GetEnvironmentVariable("SIGOV_DEV_BOOTSTRAP_TOKEN");
        var supplied = Request.Headers["X-Sigov-Dev-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(supplied) && Request.HasFormContentType)
            supplied = Request.Form["X-Sigov-Dev-Token"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(supplied) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}
