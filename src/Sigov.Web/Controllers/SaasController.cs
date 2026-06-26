using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaasController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<SaasController> _logger;

    public SaasController(PostBuildSaasService service, ILogger<SaasController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Tenants(string? busca, CancellationToken cancellationToken)
    {
        try
        {
            var tenants = await _service.ListarTenantsAsync(busca, cancellationToken).ConfigureAwait(false);
            return View(new TenantsViewModel { Tenants = tenants, Busca = busca ?? string.Empty });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela de tenants.");
            return View(new TenantsViewModel { MensagemFallback = "Não foi possível consultar tenants agora." });
        }
    }

    [HttpGet]
    public IActionResult Planos() => View();

    [HttpGet]
    public IActionResult Implantacao(long? tenantId) => View(tenantId ?? 0);

    [HttpGet]
    public IActionResult Parametros(long? tenantId) => View(tenantId ?? 0);

    [HttpGet("Tenants/{id:long}/Assinatura")]
    public IActionResult Assinatura(long id) => View(id);

    [HttpGet]
    public async Task<IActionResult> Modulos(long? tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var modulos = await _service.ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return View(new ModulosSaasViewModel { TenantId = tenantId ?? 0, Modulos = modulos });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado na tela de módulos SaaS.");
            return View(new ModulosSaasViewModel { MensagemFallback = "Não foi possível consultar módulos agora." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarTenant([FromForm] TenantFormViewModel form, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SalvarTenantAsync(form, cancellationToken).ConfigureAwait(false);
            TempData[result.Ok ? "Success" : "Error"] = result.Mensagem;
            return RedirectToAction(nameof(Tenants));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao salvar tenant SaaS.");
            TempData["Error"] = "Não foi possível salvar o tenant agora.";
            return RedirectToAction(nameof(Tenants));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarImplantacao([FromForm] long tenantId, [FromForm] string? status, CancellationToken cancellationToken)
    {
        try
        {
            var persisted = await _service.RegistrarOperacaoVisualAsync("SAAS_IMPLANTACAO_SALVAR", new { tenantId, status }, cancellationToken).ConfigureAwait(false);
            TempData[persisted ? "Success" : "Warning"] = persisted ? "Implantação salva com auditoria." : "Implantação registrada em modo visual; banco indisponível.";
            return RedirectToAction(nameof(Implantacao), new { tenantId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao salvar implantação SaaS.");
            TempData["Error"] = "Não foi possível salvar a implantação agora.";
            return RedirectToAction(nameof(Implantacao), new { tenantId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarModulo([FromForm] long tenantId, [FromForm] string codigo, [FromForm] bool ativo, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                TempData["Error"] = "Informe o módulo para alterar status.";
                return RedirectToAction(nameof(Modulos), new { tenantId });
            }

            var persisted = await _service.AlterarModuloTenantAsync(tenantId, codigo, ativo, cancellationToken).ConfigureAwait(false);
            TempData[persisted ? "Success" : "Warning"] = persisted ? "Módulo atualizado e auditado." : "Estrutura de módulo por tenant indisponível; nenhuma alteração foi simulada.";
            return RedirectToAction(nameof(Modulos), new { tenantId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao alterar módulo SaaS.");
            TempData["Error"] = "Não foi possível alterar o módulo agora.";
            return RedirectToAction(nameof(Modulos), new { tenantId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvarParametros([FromForm] long tenantId, [FromForm] string? categoria, CancellationToken cancellationToken)
    {
        try
        {
            var persisted = await _service.RegistrarOperacaoVisualAsync("SAAS_PARAMETROS_SALVAR", new { tenantId, categoria }, cancellationToken).ConfigureAwait(false);
            TempData[persisted ? "Success" : "Warning"] = persisted ? "Parâmetros salvos e auditados." : "Parâmetros mantidos como fallback visual; banco indisponível.";
            return RedirectToAction(nameof(Parametros), new { tenantId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao salvar parâmetros SaaS.");
            TempData["Error"] = "Não foi possível salvar parâmetros agora.";
            return RedirectToAction(nameof(Parametros), new { tenantId });
        }
    }

}
