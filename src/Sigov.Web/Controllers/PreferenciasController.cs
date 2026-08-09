using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Ui;
using Sigov.Infrastructure.Ui;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class PreferenciasController : Controller
{
    private const string PreferenceKey = "interface-rc47";
    private readonly UserPreferenceRepository _repository;
    private readonly IAuditTrailService _audit;
    private readonly ILogger<PreferenciasController> _logger;

    public PreferenciasController(UserPreferenceRepository repository, IAuditTrailService audit, ILogger<PreferenciasController> logger)
    {
        _repository = repository;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet("/Perfil/Preferencias")]
    [HttpGet("/Preferencias")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryContext(out var tenantId, out var userId)) return Challenge();
        var stored = await _repository.GetAsync(tenantId, userId, PreferenceKey, cancellationToken).ConfigureAwait(false);
        return View(Parse(stored?.ValueJson));
    }

    [HttpPost("/Perfil/Preferencias")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromForm] UserInterfacePreferences model, CancellationToken cancellationToken)
    {
        if (!TryContext(out var tenantId, out var userId)) return Unauthorized();
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Index", model);
        }

        var json = JsonSerializer.Serialize(model, JsonOptions);
        var previous = await _repository.GetAsync(tenantId, userId, PreferenceKey, cancellationToken).ConfigureAwait(false);
        await _repository.UpsertAsync(new UserPreferenceUpdateRequest(tenantId, userId, PreferenceKey, json), cancellationToken).ConfigureAwait(false);
        await _audit.RegistrarAsync(tenantId, userId, "PREFERENCIAS_INTERFACE_ATUALIZADAS", "sigov.usuario_preferencia", PreferenceKey,
            previous is null ? null : new { previous.ValueJson }, new { model.Theme, model.Density, model.Sidebar, model.HomePage },
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Preferências de interface atualizadas. TenantId={TenantId} UserId={UserId} CorrelationId={CorrelationId}", tenantId, userId, HttpContext.TraceIdentifier);
        if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
            return Ok(new { success = true, message = "Preferências salvas e aplicadas." });

        TempData["Success"] = "Preferências salvas e aplicadas.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryContext(out long tenantId, out long userId) =>
        long.TryParse(User.FindFirstValue("tenant_id"), out tenantId) && tenantId > 0 &&
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;

    private static UserInterfacePreferences Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<UserInterfacePreferences>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed class UserInterfacePreferences
{
    [Required, RegularExpression("light|dark|auto")]
    public string Theme { get; set; } = "auto";

    [Required, RegularExpression("comfortable|compact")]
    public string Density { get; set; } = "comfortable";

    [Required, RegularExpression("expanded|compact")]
    public string Sidebar { get; set; } = "expanded";

    [Required, RegularExpression("/MinhaCentral|/Dashboard|/Tarefas|/Protocolo")]
    public string HomePage { get; set; } = "/MinhaCentral";

    public bool NotifyProtocol { get; set; } = true;
    public bool NotifyGed { get; set; } = true;
    public bool NotifyTasks { get; set; } = true;
    public bool NotifyLgpd { get; set; } = true;
    public bool SilenceNonCritical { get; set; }
}
