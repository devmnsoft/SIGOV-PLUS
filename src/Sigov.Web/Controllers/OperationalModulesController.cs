using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class OperationalModulesController : Controller
{
    private readonly OperationalDemoService _demo;
    private readonly ILogger<OperationalModulesController> _logger;

    public OperationalModulesController(OperationalDemoService demo, ILogger<OperationalModulesController> logger)
    {
        _demo = demo;
        _logger = logger;
    }

    [Route("/{module:regex(^(Contratos|Juridico|Varejo|Atacado|Estoque|OrdemServico|Manutencao)$)}")]
    [Route("/{module:regex(^(Contratos|Juridico|Varejo|Atacado|Estoque|OrdemServico|Manutencao)$)}/{screen}")]
    [Route("/{module:regex(^(Contratos|Juridico|OrdemServico)$)}/Detalhes/{id:long}")]
    public IActionResult Module(string module, string screen = "Dashboard", long? id = null, string? q = null)
    {
        try
        {
            var vm = _demo.Build(module, id.HasValue ? $"Detalhes #{id}" : screen, q);
            return View("~/Views/Operational/Module.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar módulo operacional {Module}/{Screen}", module, screen);
            TempData["Error"] = "Não foi possível carregar os dados reais agora. Exibimos uma visão demonstrativa segura.";
            return View("~/Views/Operational/Module.cshtml", _demo.Build(module, "Em implantação"));
        }
    }

    [HttpPost("/{module}/BulkAction")]
    public IActionResult BulkAction(string module, string actionName)
    {
        try
        {
            _logger.LogInformation("Auditoria: ação em massa {Action} solicitada no módulo {Module}", actionName, module);
            TempData["Success"] = $"Ação '{actionName}' registrada para auditoria.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha em ação em massa {Action} no módulo {Module}", actionName, module);
            TempData["Error"] = "A ação não pôde ser concluída. Tente novamente ou acione o suporte.";
        }
        return RedirectToAction("Module", new { module });
    }

    [HttpGet("/{module}/ExportCsv")]
    public IActionResult ExportCsv(string module)
    {
        try
        {
            var rows = _demo.Build(module).Records;
            var csv = "Id;Codigo;Nome;Status;Responsavel;AtualizadoEm\n" + string.Join("\n", rows.Select(r => $"{r.Id};{r.Codigo};{r.Nome};{r.Status};{r.Responsavel};{r.AtualizadoEm}"));
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{module.ToLowerInvariant()}-demo.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao exportar CSV do módulo {Module}", module);
            TempData["Error"] = "Exportação indisponível no momento.";
            return RedirectToAction("Module", new { module });
        }
    }
}
