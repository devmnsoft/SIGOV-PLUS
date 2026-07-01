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


    [HttpGet("Saas/Tenants/Novo")]
    public IActionResult NovoTenant() => View("Tenants", new TenantsViewModel { MensagemFallback = "Preencha o formulário para persistir em sigov.tenant quando a tabela existir." });

    [HttpPost("Saas/Tenants/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoTenant([FromForm] TenantFormViewModel form, CancellationToken cancellationToken) => await SalvarTenant(form, cancellationToken).ConfigureAwait(false);

    [HttpGet("Saas/Tenants/{id:long}")]
    public async Task<IActionResult> DetalheTenant(long id, CancellationToken cancellationToken)
    {
        var tenants = await _service.ListarTenantsAsync(null, cancellationToken).ConfigureAwait(false);
        var tenant = tenants.FirstOrDefault(x => x.Id == id);
        if (tenant is null) TempData["Warning"] = "Tenant não encontrado ou estrutura indisponível.";
        return View("Tenants", new TenantsViewModel { Tenants = tenant is null ? Array.Empty<TenantListItemViewModel>() : new[] { tenant } });
    }

    [HttpGet("Saas/Tenants/{id:long}/Editar")]
    public async Task<IActionResult> EditarTenant(long id, CancellationToken cancellationToken) => await DetalheTenant(id, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Tenants/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarTenant(long id, [FromForm] TenantFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return await SalvarTenant(form, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("Saas/Modulos/{codigo}")]
    public async Task<IActionResult> ModuloDetalhe(string codigo, long? tenantId, CancellationToken cancellationToken)
    {
        var modulos = await _service.ListarModulosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return View("Modulos", new ModulosSaasViewModel { TenantId = tenantId ?? 0, Modulos = modulos.Where(x => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase)).ToArray(), MensagemFallback = "Detalhe técnico do módulo; contratação só é persistida se a tabela tenant_modulo_contratado existir." });
    }

    [HttpPost("Saas/Modulos/{codigo}/Ativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarModulo(string codigo, [FromForm] long tenantId, CancellationToken cancellationToken) => await AlterarModulo(tenantId, codigo, true, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Modulos/{codigo}/Inativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InativarModulo(string codigo, [FromForm] long tenantId, CancellationToken cancellationToken) => await AlterarModulo(tenantId, codigo, false, cancellationToken).ConfigureAwait(false);

    [HttpGet]
    public IActionResult Planos() => View();

    [HttpGet]
    public IActionResult Implantacao(long? tenantId) => View(tenantId ?? 0);

    [HttpGet]
    public async Task<IActionResult> Parametros(long? tenantId, string? categoria, string? escopo, string? busca, CancellationToken cancellationToken)
    {
        var id = tenantId ?? 0;
        var model = await _service.ListarParametrosAsync(id, categoria, escopo, busca, cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    [HttpGet("Saas/Parametros/{id:long}/Editar")]
    public async Task<IActionResult> EditarParametro(long id, long? tenantId, string? categoria, string? escopo, string? busca, CancellationToken cancellationToken)
    {
        TempData["Warning"] = "Edite o valor no formulário de parâmetros. A gravação só ocorre em sigov.parametro_sistema quando a chave existir no schema real.";
        return await Parametros(tenantId, categoria, escopo, busca, cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("Saas/Parametros/Editar")]
    public async Task<IActionResult> EditarParametroPorChave(string? chave, long? tenantId, string? escopo, CancellationToken cancellationToken)
    {
        TempData["Warning"] = string.IsNullOrWhiteSpace(chave) ? "Informe a chave do parâmetro." : $"Editando parâmetro {chave}; confirme valor e tipo antes de salvar.";
        return await Parametros(tenantId, null, escopo, chave, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("Saas/Parametros/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarParametro(long id, [FromForm] ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        return await SalvarParametros(form, cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("Saas/Parametros/{id:long}/RestaurarPadrao")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestaurarPadraoParametro(long id, [FromForm] ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        form.Id = id;
        var result = await _service.RestaurarParametroPadraoAsync(form, cancellationToken).ConfigureAwait(false);
        TempData[result.Ok ? "Success" : "Error"] = result.Mensagem;
        return RedirectToAction(nameof(Parametros), new { tenantId = form.TenantId, escopo = form.Escopo });
    }

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
    public async Task<IActionResult> SalvarParametros([FromForm] ParametroSaasFormViewModel form, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SalvarParametroAsync(form, cancellationToken).ConfigureAwait(false);
            TempData[result.Ok ? "Success" : "Error"] = result.Mensagem;
            return RedirectToAction(nameof(Parametros), new { tenantId = form.TenantId, escopo = form.Escopo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao salvar parâmetros SaaS.");
            TempData["Error"] = "Não foi possível salvar parâmetros agora.";
            return RedirectToAction(nameof(Parametros), new { tenantId = form.TenantId, escopo = form.Escopo });
        }
    }

    [HttpPost("Saas/Tenants/{id:long}/Ativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarTenant(long id, CancellationToken cancellationToken) =>
        await AlterarStatusTenant(id, true, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Tenants/{id:long}/Inativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InativarTenant(long id, CancellationToken cancellationToken) =>
        await AlterarStatusTenant(id, false, cancellationToken).ConfigureAwait(false);

    private async Task<IActionResult> AlterarStatusTenant(long id, bool ativo, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _service.AlterarStatusTenantAsync(id, ativo, cancellationToken).ConfigureAwait(false);
            TempData[ok ? "Success" : "Error"] = ok
                ? (ativo ? "Tenant ativado e auditado." : "Tenant inativado e auditado.")
                : "Tenant não foi alterado; nenhum sucesso foi simulado.";
            return RedirectToAction(nameof(Tenants));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro tratado ao alterar status de tenant. TenantId={TenantId}", id);
            TempData["Error"] = "Não foi possível alterar o tenant agora.";
            return RedirectToAction(nameof(Tenants));
        }
    }

}
