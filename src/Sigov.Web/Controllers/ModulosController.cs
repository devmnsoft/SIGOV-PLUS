using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Commercial;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ModulosController : Controller
{
    private readonly IModuleCatalogService _moduleCatalogService;
    private readonly IModuloAccessService _access;
    private readonly IAuditTrailService _audit;

    public ModulosController(IModuleCatalogService moduleCatalogService, IModuloAccessService access, IAuditTrailService audit)
    {
        _moduleCatalogService = moduleCatalogService;
        _access = access;
        _audit = audit;
    }

    public IActionResult Index() => RedirectToAction(nameof(Catalogo));

    [HttpGet("/Modulos/Catalogo")]
    public IActionResult Catalogo() => View("Catalogo", _access.BuildCatalog(User, includeBlocked: true));

    [HttpGet("/Modulos/MeuAcesso")]
    public IActionResult MeuAcesso() => View("Catalogo", _access.BuildCatalog(User, includeBlocked: false));

    public async Task<IActionResult> Detalhe(string id, CancellationToken cancellationToken)
    {
        var module = _moduleCatalogService.FindByCode(id);
        if (module is null) return NotFound();
        if (_access.CanAccess(User, module)) return View(module);
        await _audit.RegistrarAsync(ClaimLong("tenant_id"), ClaimLong("usuario_id"), "ACESSO_NEGADO", "modulo", module.Code,
            null, new { modulo = module.Code, recurso = "catalogo", acao = "acessar", motivo = "sem_permissao_ou_contrato" },
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        return Forbid();
    }

    private long? ClaimLong(string type) => long.TryParse(User.FindFirstValue(type), out var value) ? value : null;
}
