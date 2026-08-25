using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Juridico;
using Sigov.Web.Models.Juridico;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Juridico")]
public sealed class JuridicoController(
    IJuridicoRepository repository,
    IAuthorizationService authorization,
    ILogger<JuridicoController> logger) : Controller
{
    private static readonly IReadOnlyDictionary<string, (string Title, string View, string Manage)> Resources = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["advogados"]=("Advogados e procuradores","JURIDICO_ADVOGADO_VIEW","JURIDICO_ADVOGADO_MANAGE"), ["partes"]=("Partes","JURIDICO_PARTE_VIEW","JURIDICO_PARTE_MANAGE"), ["processos"]=("Processos jurídicos","JURIDICO_PROCESSO_VIEW","JURIDICO_PROCESSO_MANAGE"), ["movimentacoes"]=("Movimentações","JURIDICO_MOVIMENTACAO_VIEW","JURIDICO_MOVIMENTACAO_MANAGE"), ["prazos"]=("Prazos","JURIDICO_PRAZO_VIEW","JURIDICO_PRAZO_MANAGE"), ["intimacoes"]=("Intimações","JURIDICO_INTIMACAO_VIEW","JURIDICO_INTIMACAO_MANAGE"), ["audiencias"]=("Audiências","JURIDICO_AUDIENCIA_VIEW","JURIDICO_AUDIENCIA_MANAGE"), ["pareceres"]=("Pareceres","JURIDICO_PARECER_VIEW","JURIDICO_PARECER_MANAGE"), ["consultas"]=("Consultas jurídicas","JURIDICO_CONSULTA_VIEW","JURIDICO_CONSULTA_MANAGE"), ["acordos"]=("Acordos","JURIDICO_ACORDO_VIEW","JURIDICO_ACORDO_MANAGE"), ["dividaativa"]=("Dívida ativa judicial","JURIDICO_DIVIDA_ATIVA_VIEW","JURIDICO_DIVIDA_ATIVA_MANAGE"), ["custas"]=("Custas","JURIDICO_PROCESSO_VIEW","JURIDICO_PROCESSO_MANAGE"), ["auditoria"]=("Auditoria","JURIDICO_AUDITORIA_VIEW",string.Empty)
    };

    [HttpGet("")]
    [HttpGet("Dashboard")]
    [Authorize(Policy = "JURIDICO_DASHBOARD_VIEW")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View("Dashboard", await repository.DashboardAsync(Context(), ct)); }
        catch (Exception ex) { return FunctionalError(ex, "carregar o dashboard"); }
    }

    [HttpGet("{recurso:regex(^Advogados|Partes|Processos|Movimentacoes|Prazos|Intimacoes|Audiencias|Pareceres|Consultas|Acordos|DividaAtiva|Custas|Auditoria$)}")]
    public async Task<IActionResult> Lista(string recurso, string? busca, string? status, int pagina, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out var meta)) return NotFound();
        if (!await Allowed(meta.View)) return Forbid();
        try
        {
            var filter = new JuridicoFiltro(busca, status, Math.Max(1, pagina));
            return View("Lista", new JuridicoListaViewModel(meta.Title, key, await repository.ListarAsync(Context(), key, filter, ct), filter, key != "auditoria"));
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
        return View("Form", new JuridicoRegistroRequest("", "", DefaultStatus(key), null));
    }

    [HttpPost("{recurso}/Salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(string recurso, JuridicoRegistroRequest model, long? id, CancellationToken ct)
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
    [Authorize(Policy = "JURIDICO_RELATORIO_EXPORT")]
    public IActionResult Relatorios() => View();

    [HttpGet("Relatorios/{recurso}.csv")]
    [Authorize(Policy = "JURIDICO_RELATORIO_EXPORT")]
    public async Task<IActionResult> Csv(string recurso, CancellationToken ct)
    {
        if (!TryMeta(recurso, out var key, out _)) return BadRequest("Recurso não exportável.");
        try
        {
            return File(await repository.ExportarCsvAsync(Context(), key, ct), "text/csv; charset=utf-8", $"juridico-{key}-{DateTime.UtcNow:yyyyMMdd}.csv");
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
    private static string DefaultStatus(string resource) => resource switch { "processos" or "pareceres" => "RASCUNHO", "movimentacoes" => "REGISTRADA", "prazos" => "ABERTO", "intimacoes" => "RECEBIDA", "audiencias" => "AGENDADA", "acordos" => "MINUTA", "dividaativa" => "AJUIZADA", "custas" => "PENDENTE", _ => "ATIVO" };
    private void SetFormData(string resource, string title) { ViewData["Recurso"] = resource; ViewData["Titulo"] = title; }
    private JuridicoContexto Context()
    {
        if (!long.TryParse(User.FindFirst("tenant_id")?.Value, out var tenant) || tenant <= 0 ||
            !long.TryParse(User.FindFirst("entidade_id")?.Value, out var entity) || entity <= 0)
            throw new UnauthorizedAccessException("Tenant/entidade não resolvidos.");
        long.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var user);
        return new(tenant, entity, user > 0 ? user : null, HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());
    }
}
