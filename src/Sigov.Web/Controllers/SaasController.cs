using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;
using Sigov.Web.Models.PostBuild;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaasController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly IAuthorizationEvaluator _authorization;
    private readonly ILogger<SaasController> _logger;

    public SaasController(PostBuildSaasService service, IAuthorizationEvaluator authorization, ILogger<SaasController> logger)
    {
        _service = service;
        _authorization = authorization;
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
    public async Task<IActionResult> Planos(CancellationToken cancellationToken) => View(await _service.ListarPlanosAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("Saas/Planos/Novo")]
    public async Task<IActionResult> NovoPlano(CancellationToken cancellationToken) { TempData["Warning"] = "O catálogo de planos é mantido em sigov.saas_plano como fonte de autoridade; nenhum plano é criado nesta tela."; return View("Planos", await _service.ListarPlanosAsync(cancellationToken).ConfigureAwait(false)); }

    [HttpGet("Saas/Planos/{id:long}")]
    public async Task<IActionResult> DetalhePlano(long id, CancellationToken cancellationToken) => View("Planos", await _service.ListarPlanosAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("Saas/Planos/{id:long}/Editar")]
    public async Task<IActionResult> EditarPlano(long id, CancellationToken cancellationToken) { TempData["Warning"] = "A edição de planos ocorre em sigov.saas_plano como fonte de autoridade; esta tela exibe o catálogo persistido."; return View("Planos", await _service.ListarPlanosAsync(cancellationToken).ConfigureAwait(false)); }

    [HttpGet("Saas/Assinaturas")]
    public async Task<IActionResult> Assinaturas(CancellationToken cancellationToken)
    {
        ViewBag.Planos = (await _service.ListarPlanosAsync(cancellationToken).ConfigureAwait(false)).Planos;
        return View(await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("Saas/Assinaturas/Nova")]
    public async Task<IActionResult> NovaAssinatura(CancellationToken cancellationToken) => View("Assinaturas", await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("Saas/Assinaturas/{id:long}")]
    public async Task<IActionResult> DetalheAssinatura(long id, CancellationToken cancellationToken) => View("Assinaturas", await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("Saas/Assinaturas/{id:long}/Editar")]
    public async Task<IActionResult> EditarAssinatura(long id, CancellationToken cancellationToken) { TempData["Warning"] = "Use as ações da assinatura (upgrade, downgrade, suspender, reativar, cancelar); nenhuma edição é simulada."; return View("Assinaturas", await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false)); }

    [HttpPost("Saas/Assinaturas/Nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovaAssinaturaPost([FromForm] long TenantId, [FromForm] long PlanoId, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;
        if (TenantId <= 0 || PlanoId <= 0)
        {
            TempData["Error"] = "Informe o tenant e o plano para criar a assinatura.";
            return RedirectToAction(nameof(Assinaturas));
        }
        try
        {
            var resultado = await _service.CriarAssinaturaComercialAsync(TenantId, PlanoId, CurrentUserId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            TempData[resultado.Sucesso ? "Success" : "Error"] = resultado.Mensagem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar assinatura comercial para tenant {TenantId}.", TenantId);
            TempData["Error"] = "Não foi possível criar a assinatura agora.";
        }
        return RedirectToAction(nameof(Assinaturas));
    }

    [HttpPost("Saas/Assinaturas/Tenants/{tenantId:long}/Upgrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpgradeAssinatura(long tenantId, [FromForm] long NovoPlanoId, [FromForm] string? Motivo, CancellationToken cancellationToken) =>
        await TrocarPlano(tenantId, NovoPlanoId, Motivo, upgrade: true, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Assinaturas/Tenants/{tenantId:long}/Downgrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DowngradeAssinatura(long tenantId, [FromForm] long NovoPlanoId, [FromForm] string? Motivo, CancellationToken cancellationToken) =>
        await TrocarPlano(tenantId, NovoPlanoId, Motivo, upgrade: false, cancellationToken).ConfigureAwait(false);

    private async Task<IActionResult> TrocarPlano(long tenantId, long novoPlanoId, string? motivo, bool upgrade, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;
        if (tenantId <= 0 || novoPlanoId <= 0)
        {
            TempData["Error"] = "Informe o tenant e o plano destino para alterar a assinatura.";
            return RedirectToAction(nameof(Assinaturas));
        }
        try
        {
            var resultado = await _service.TrocarPlanoComercialAsync(tenantId, novoPlanoId, motivo, upgrade, CurrentUserId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            TempData[resultado.Sucesso ? "Success" : "Error"] = resultado.Mensagem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro em {Operacao} da assinatura do tenant {TenantId}.", upgrade ? "upgrade" : "downgrade", tenantId);
            TempData["Error"] = "Não foi possível alterar o plano da assinatura agora.";
        }
        return RedirectToAction(nameof(Assinaturas));
    }

    [HttpPost("Saas/Assinaturas/{id:long}/Suspender")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspenderAssinatura(long id, [FromForm] string? Motivo, CancellationToken cancellationToken) =>
        await AlterarStatusAssinatura(id, "SUSPENSA", Motivo, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Assinaturas/{id:long}/Reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReativarAssinatura(long id, [FromForm] string? Motivo, CancellationToken cancellationToken) =>
        await AlterarStatusAssinatura(id, "ATIVA", Motivo, cancellationToken).ConfigureAwait(false);

    [HttpPost("Saas/Assinaturas/{id:long}/Cancelar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarAssinatura(long id, [FromForm] string? Motivo, CancellationToken cancellationToken) =>
        await AlterarStatusAssinatura(id, "CANCELADA", Motivo, cancellationToken).ConfigureAwait(false);

    private async Task<IActionResult> AlterarStatusAssinatura(long id, string statusDestino, string? motivo, CancellationToken cancellationToken)
    {
        var denied = await RequireSigovAdminAsync(cancellationToken).ConfigureAwait(false);
        if (denied is not null) return denied;
        try
        {
            var resultado = await _service.AlterarStatusAssinaturaPorIdAsync(id, statusDestino, motivo, CurrentUserId(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
            TempData[resultado.Sucesso ? "Success" : "Error"] = resultado.Mensagem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar status da assinatura {AssinaturaId}.", id);
            TempData["Error"] = "Não foi possível alterar o status da assinatura agora.";
        }
        return RedirectToAction(nameof(Assinaturas));
    }

    private long CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("usuario_id");
        return long.TryParse(raw, CultureInfo.InvariantCulture, out var userId) ? userId : 0;
    }

    private async Task<IActionResult?> RequireSigovAdminAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId <= 0) return Forbid();
        long? tenantId = long.TryParse(User.FindFirstValue("tenant_id"), CultureInfo.InvariantCulture, out var tenantClaim) && tenantClaim > 0 ? tenantClaim : null;
        var decision = await _authorization.EvaluateAsync(new AuthorizationRequest(
            userId, "saas", "plataforma", "administrar", tenantId,
            CorrelationId: HttpContext.TraceIdentifier, Origem: "WEB_SAAS_COMERCIAL"), cancellationToken).ConfigureAwait(false);
        return decision.Permitido ? null : Forbid();
    }

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

    [HttpGet("Saas/Tenants/{id:long}/Assinatura")]
    public async Task<IActionResult> Assinatura(long id, CancellationToken cancellationToken) => View("Assinaturas", await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("Saas/Tenants/{id:long}/Modulos")]
    public async Task<IActionResult> TenantModulos(long id, CancellationToken cancellationToken) => View("Modulos", new ModulosSaasViewModel { TenantId = id, Modulos = await _service.ListarModulosAsync(id, cancellationToken).ConfigureAwait(false) });

    [HttpGet("Saas/Tenants/{id:long}/WhiteLabel")]
    public IActionResult TenantWhiteLabel(long id) => View("Assinatura", id);

    [HttpGet("Saas/Tenants/{id:long}/Uso")]
    public IActionResult TenantUso(long id) => View("Assinatura", id);

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
