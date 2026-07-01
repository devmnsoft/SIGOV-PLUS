using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class PortalController : Controller
{
    private readonly PostBuildSaasService _service;
    public PortalController(PostBuildSaasService service) => _service = service;
    [HttpGet("/Portal")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));
    [HttpGet("/Portal/MinhaAssinatura")] public async Task<IActionResult> MinhaAssinatura(CancellationToken cancellationToken) => View("Index", await _service.ListarAssinaturasAsync(cancellationToken).ConfigureAwait(false));
    [HttpGet("/Portal/MeusModulos")] public async Task<IActionResult> MeusModulos(CancellationToken cancellationToken) { ViewBag.PortalSecao="Módulos contratados"; return View("Modulos", new Sigov.Web.Models.PostBuild.ModulosSaasViewModel { Modulos = await _service.ListarModulosAsync(null, cancellationToken).ConfigureAwait(false) }); }
    [HttpGet("/Portal/Usuarios")] public IActionResult Usuarios() => View("Fallback", "Usuários do portal dependem da matriz de permissões do tenant; nenhum cadastro é simulado.");
    [HttpGet("/Portal/Suporte")] public IActionResult Suporte() => View("Fallback", "Abertura de chamado será habilitada quando houver tabela de suporte/tickets.");
    [HttpGet("/Portal/Faturas")] public IActionResult Faturas() => View("Fallback", "Faturas dependem do módulo financeiro/billing persistente; nenhum boleto é simulado.");
}
