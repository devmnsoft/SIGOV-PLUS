using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MobileController : EnterprisePageControllerBase
{
    public MobileController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment) : base(service, tenantContext, environment) { }
    public IActionResult Index() => RedirectToAction(nameof(Home));
    public IActionResult Login() => View("~/Views/Mobile/Login.cshtml");
    public IActionResult Home() => MobilePage("Início Mobile", "mobile.acessar", "/api/campo/dashboard");
    public IActionResult Offline() => View("~/Views/Mobile/Offline.cshtml");
    public IActionResult Sync() => MobilePage("Sincronização", "mobile.sincronizar", "/api/mobile/sync/lotes");
    public IActionResult Agenda() => MobilePage("Agenda Mobile", "campo.atividades.visualizar", "/api/campo/atividades");
    public IActionResult Atividades(long? id) => MobilePage(id.HasValue ? "Detalhe da Atividade" : "Atividades Mobile", "campo.atividades.visualizar", id.HasValue ? $"/api/campo/atividades/{id.Value}" : "/api/campo/atividades");
    public IActionResult Checklists() => MobilePage("Checklists", "campo.checklists.visualizar", "/api/campo/checklists");
    public IActionResult Evidencias() => MobilePage("Evidências", "campo.evidencias.enviar", "/api/campo/atividades/1/evidencias");
    public IActionResult Assinatura() => MobilePage("Assinatura em Campo", "campo.assinatura.coletar", "/api/campo/atividades/1/assinaturas");
    public IActionResult Mapa() => MobilePage("Mapa e Localizações", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
    public IActionResult Notificacoes() => MobilePage("Notificações", "campo.notificacoes.visualizar", "/api/campo/notificacoes");
    private IActionResult MobilePage(string title, string permission, string apiRoute) => ModulePage("mobile_pwa", title, permission, apiRoute);
}

[Authorize]
public sealed class CampoController : EnterprisePageControllerBase
{
    public CampoController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment) : base(service, tenantContext, environment) { }
    public IActionResult Dashboard() => ModulePage("campo_operacional", "Dashboard Campo", "campo.dashboard.visualizar", "/api/campo/dashboard");
    public IActionResult Atividades() => ModulePage("campo_operacional", "Atividades de Campo", "campo.atividades.visualizar", "/api/campo/atividades");
    public IActionResult Visitas() => ModulePage("campo_operacional", "Visitas Técnicas", "campo.visitas.visualizar", "/api/campo/visitas");
    public IActionResult Rotas() => ModulePage("campo_operacional", "Rotas de Campo", "campo.rotas.visualizar", "/api/campo/rotas");
    public IActionResult Checklists() => ModulePage("campo_operacional", "Checklists de Campo", "campo.checklists.visualizar", "/api/campo/checklists");
    public IActionResult Formularios() => ModulePage("campo_operacional", "Formulários de Campo", "campo.formularios.visualizar", "/api/campo/formularios");
    public IActionResult Dispositivos() => ModulePage("campo_operacional", "Dispositivos Mobile", "mobile.dispositivo.gerenciar", "/api/mobile/dispositivos");
    public IActionResult Sincronizacao() => ModulePage("offline_sync", "Sincronização Offline", "campo.sincronizacao.visualizar", "/api/mobile/sync/lotes");
    public IActionResult Notificacoes() => ModulePage("notificacoes_mobile", "Notificações de Campo", "campo.notificacoes.visualizar", "/api/campo/notificacoes");
    public IActionResult Mapa() => ModulePage("georreferenciamento", "Mapa de Campo", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
    public IActionResult Localizacoes() => ModulePage("georreferenciamento", "Localizações", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
}

[AllowAnonymous]
public sealed class OfflineController : Controller
{
    public IActionResult Index() => View();
}


[AllowAnonymous]
public sealed class MobileCampoController : Controller
{
    private readonly Sigov.Web.Services.SectorModuleService _service;
    public MobileCampoController(Sigov.Web.Services.SectorModuleService service) => _service = service;
    [Route("/MobileCampo")]
    [Route("/MobileCampo/{pagina}")]
    [Route("/MobileCampo/Inventario")]
    [Route("/MobileCampo/Obras")]
    [Route("/MobileCampo/Evidencias")]
    [Route("/MobileCampo/Sincronizacao")]
    [Route("/MobileCampo/Dispositivos")]
    [Route("/MobileCampo/Roteiros")]
    [Route("/MobileCampo/Coletas")]
    [Route("/MobileCampo/Conflitos")]
    public async Task<IActionResult> Index(string? pagina, string? q, CancellationToken cancellationToken) => View("~/Views/Sectors/Module.cshtml", await _service.BuildAsync("Mobile/Campo", $"Mobile/Campo{(pagina is null ? "" : " — " + pagina)}", "Roteiros, coletas, evidências e sincronização offline planejada sem simulação.", new[] { "Roteiros", "Coletas", "Evidências", "Sincronização" }, new[] { "/MobileCampo/Roteiros", "/MobileCampo/Coletas", "/MobileCampo/Evidencias", "/MobileCampo/Sincronizacao" }, true, q, cancellationToken));
}
