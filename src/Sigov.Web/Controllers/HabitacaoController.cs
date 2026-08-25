using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Habitacao;
using Sigov.Web.Models.Habitacao;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Habitacao")]
public sealed class HabitacaoController(
    IHabitacaoRepository repository,
    IAuthorizationService authorization,
    ILogger<HabitacaoController> logger) : Controller
{
    private static readonly IReadOnlyDictionary<string, (string Title, string View, string Manage)> Resources =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["familias"] = ("Famílias", "HABITACAO_FAMILIA_VIEW", "HABITACAO_FAMILIA_MANAGE"),
            ["membros"] = ("Membros familiares", "HABITACAO_FAMILIA_VIEW", "HABITACAO_FAMILIA_MANAGE"),
            ["domicilios"] = ("Domicílios", "HABITACAO_DOMICILIO_VIEW", "HABITACAO_DOMICILIO_MANAGE"),
            ["programas"] = ("Programas habitacionais", "HABITACAO_PROGRAMA_VIEW", "HABITACAO_PROGRAMA_MANAGE"),
            ["inscricoes"] = ("Inscrições", "HABITACAO_INSCRICAO_VIEW", "HABITACAO_INSCRICAO_MANAGE"),
            ["classificacao"] = ("Classificação", "HABITACAO_CLASSIFICACAO_VIEW", "HABITACAO_CLASSIFICACAO_MANAGE"),
            ["visitas"] = ("Visitas sociais e técnicas", "HABITACAO_VISITA_VIEW", "HABITACAO_VISITA_MANAGE"),
            ["regularizacao"] = ("Regularização fundiária", "HABITACAO_REGULARIZACAO_VIEW", "HABITACAO_REGULARIZACAO_MANAGE"),
            ["nucleos"] = ("Núcleos urbanos", "HABITACAO_REGULARIZACAO_VIEW", "HABITACAO_REGULARIZACAO_MANAGE"),
            ["lotes"] = ("Lotes", "HABITACAO_LOTE_VIEW", "HABITACAO_LOTE_MANAGE"),
            ["unidades"] = ("Unidades habitacionais", "HABITACAO_UNIDADE_VIEW", "HABITACAO_UNIDADE_MANAGE"),
            ["beneficiarios"] = ("Beneficiários e termos", "HABITACAO_BENEFICIARIO_VIEW", "HABITACAO_BENEFICIARIO_MANAGE"),
            ["auditoria"] = ("Auditoria", "HABITACAO_AUDITORIA_VIEW", string.Empty)
        };

    [HttpGet("")]
    [HttpGet("Dashboard")]
    [Authorize(Policy = "HABITACAO_DASHBOARD_VIEW")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View("Dashboard", await repository.DashboardAsync(Context(), ct)); }
        catch (Exception ex) { return FunctionalError(ex, "carregar o dashboard"); }
    }

    [HttpGet("{recurso:regex(^Familias|Membros|Domicilios|Programas|Inscricoes|Classificacao|Visitas|Regularizacao|Nucleos|Lotes|Unidades|Beneficiarios|Auditoria$)}")]
    public async Task<IActionResult> Lista(string recurso, string? busca, string? status, int pagina, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out var meta)) return NotFound();
        if (!await Allowed(meta.View)) return Forbid();
        try
        {
            var filter = new HabitacaoFiltro(busca, status, Math.Max(1, pagina));
            return View("Lista", new HabitacaoListaViewModel(meta.Title, key, await repository.ListarAsync(Context(), key, filter, ct), filter, key != "auditoria"));
        }
        catch (Exception ex) { return FunctionalError(ex, $"listar {key}"); }
    }

    [HttpGet("{recurso}/Novo")]
    public async Task<IActionResult> Novo(string recurso)
    {
        if (!TryMeta(recurso, out var key, out var meta)) return NotFound();
        if (key == "auditoria") return BadRequest("Auditoria é somente leitura.");
        if (!await Allowed(meta.Manage)) return Forbid();
        SetFormData(key, meta.Title);
        return View("Form", new HabitacaoRegistroRequest("", "", DefaultStatus(key), null));
    }

    [HttpPost("{recurso}/Salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(string recurso, HabitacaoRegistroRequest model, long? id, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out var meta)) return NotFound();
        if (key == "auditoria") return BadRequest("Auditoria é somente leitura.");
        if (!await Allowed(meta.Manage)) return Forbid();
        try
        {
            await repository.SalvarAsync(Context(), key, model, id, ct);
            TempData["Success"] = "Registro salvo com histórico e auditoria.";
            return RedirectToAction(nameof(Lista), new { recurso = Route(key) });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            logger.LogWarning(ex, "Falha funcional ao salvar {Recurso}. CorrelationId {CorrelationId}", key, HttpContext.TraceIdentifier);
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao salvar {Recurso}. CorrelationId {CorrelationId}", key, HttpContext.TraceIdentifier);
            ModelState.AddModelError(string.Empty, $"Não foi possível salvar. Referência: {HttpContext.TraceIdentifier}");
        }
        SetFormData(key, meta.Title);
        return View("Form", model);
    }

    [HttpPost("{recurso}/{id:long}/Excluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Excluir(string recurso, long id, string justificativa, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out var meta)) return NotFound();
        if (key == "auditoria") return BadRequest("Auditoria é somente leitura.");
        if (!await Allowed(meta.Manage)) return Forbid();
        if (string.IsNullOrWhiteSpace(justificativa)) return BadRequest("Justificativa é obrigatória para exclusão.");
        try
        {
            await repository.ExcluirAsync(Context(), key, id, justificativa, ct);
            TempData["Success"] = "Exclusão lógica auditada.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao excluir {Recurso}. CorrelationId {CorrelationId}", key, HttpContext.TraceIdentifier);
            TempData["Error"] = $"Não foi possível excluir. Referência: {HttpContext.TraceIdentifier}";
        }
        return RedirectToAction(nameof(Lista), new { recurso = Route(key) });
    }

    [HttpGet("Relatorios")]
    [Authorize(Policy = "HABITACAO_RELATORIO_EXPORT")]
    public IActionResult Relatorios() => View();

    [HttpGet("Relatorios/{recurso}.csv")]
    [Authorize(Policy = "HABITACAO_RELATORIO_EXPORT")]
    public async Task<IActionResult> Csv(string recurso, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out _) || key == "auditoria") return BadRequest("Recurso não exportável.");
        try
        {
            return File(await repository.ExportarCsvAsync(Context(), key, ct), "text/csv; charset=utf-8", $"habitacao-{key}-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex) { return FunctionalError(ex, $"exportar {key}"); }
    }

    private IActionResult FunctionalError(Exception ex, string operation)
    {
        logger.LogError(ex, "Falha ao {Operation}. CorrelationId {CorrelationId}", operation, HttpContext.TraceIdentifier);
        return Problem($"Não foi possível {operation}. Referência: {HttpContext.TraceIdentifier}", statusCode: StatusCodes.Status500InternalServerError);
    }

    private async Task<bool> Allowed(string policy) => !string.IsNullOrEmpty(policy) && (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    private static bool TryMeta(string resource, out string key, out (string Title, string View, string Manage) meta)
    {
        key = resource.ToLowerInvariant();
        return Resources.TryGetValue(key, out meta);
    }
    private static string Route(string resource) => char.ToUpperInvariant(resource[0]) + resource[1..];
    private static string DefaultStatus(string resource) => resource switch
    {
        "inscricoes" => "RASCUNHO",
        "visitas" => "AGENDADA",
        "regularizacao" => "LEVANTAMENTO",
        "lotes" or "unidades" => "DISPONIVEL",
        "beneficiarios" => "SELECIONADO",
        _ => "ATIVO"
    };
    private void SetFormData(string resource, string title) { ViewData["Recurso"] = resource; ViewData["Titulo"] = title; }
    private HabitacaoContexto Context()
    {
        if (!long.TryParse(User.FindFirst("tenant_id")?.Value, out var tenant) || tenant <= 0 ||
            !long.TryParse(User.FindFirst("entidade_id")?.Value, out var entity) || entity <= 0)
            throw new UnauthorizedAccessException("Tenant/entidade não resolvidos.");
        long.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var user);
        return new(tenant, entity, user > 0 ? user : null, HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());
    }
}
