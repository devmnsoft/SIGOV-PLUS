using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MobileController : EnterprisePageControllerBase
{
    public MobileController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public IActionResult Index() => RedirectToAction(nameof(Home));
    public IActionResult Login() => View("~/Views/Mobile/Login.cshtml");
    public Task<IActionResult> Home() => MobilePage("Início Mobile", "mobile.acessar", "/api/campo/dashboard");
    public IActionResult Offline() => View("~/Views/Mobile/Offline.cshtml");
    public Task<IActionResult> Sync() => MobilePage("Sincronização", "mobile.sincronizar", "/api/mobile/sync/lotes");
    public Task<IActionResult> Agenda() => MobilePage("Agenda Mobile", "campo.atividades.visualizar", "/api/campo/atividades");
    public Task<IActionResult> Atividades(long? id) => MobilePage(id.HasValue ? "Detalhe da Atividade" : "Atividades Mobile", "campo.atividades.visualizar", id.HasValue ? $"/api/campo/atividades/{id.Value}" : "/api/campo/atividades");
    public Task<IActionResult> Checklists() => MobilePage("Checklists", "campo.checklists.visualizar", "/api/campo/checklists");
    public Task<IActionResult> Evidencias() => MobilePage("Evidências", "campo.evidencias.enviar", "/api/campo/atividades/1/evidencias");
    public Task<IActionResult> Assinatura() => MobilePage("Assinatura em Campo", "campo.assinatura.coletar", "/api/campo/atividades/1/assinaturas");
    public Task<IActionResult> Mapa() => MobilePage("Mapa e Localizações", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
    public Task<IActionResult> Notificacoes() => MobilePage("Notificações", "campo.notificacoes.visualizar", "/api/campo/notificacoes");
    private Task<IActionResult> MobilePage(string title, string permission, string apiRoute) => ModulePage("mobile_pwa", title, permission, apiRoute);
}

[Authorize]
public sealed class CampoController : EnterprisePageControllerBase
{
    public CampoController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("campo_operacional", "Dashboard Campo", "campo.dashboard.visualizar", "/api/campo/dashboard");
    public Task<IActionResult> Atividades() => ModulePage("campo_operacional", "Atividades de Campo", "campo.atividades.visualizar", "/api/campo/atividades");
    public Task<IActionResult> Visitas() => ModulePage("campo_operacional", "Visitas Técnicas", "campo.visitas.visualizar", "/api/campo/visitas");
    public Task<IActionResult> Rotas() => ModulePage("campo_operacional", "Rotas de Campo", "campo.rotas.visualizar", "/api/campo/rotas");
    public Task<IActionResult> Checklists() => ModulePage("campo_operacional", "Checklists de Campo", "campo.checklists.visualizar", "/api/campo/checklists");
    public Task<IActionResult> Formularios() => ModulePage("campo_operacional", "Formulários de Campo", "campo.formularios.visualizar", "/api/campo/formularios");
    public Task<IActionResult> Dispositivos() => ModulePage("campo_operacional", "Dispositivos Mobile", "mobile.dispositivo.gerenciar", "/api/mobile/dispositivos");
    public Task<IActionResult> Sincronizacao() => ModulePage("offline_sync", "Sincronização Offline", "campo.sincronizacao.visualizar", "/api/mobile/sync/lotes");
    public Task<IActionResult> Notificacoes() => ModulePage("notificacoes_mobile", "Notificações de Campo", "campo.notificacoes.visualizar", "/api/campo/notificacoes");
    public Task<IActionResult> Mapa() => ModulePage("georreferenciamento", "Mapa de Campo", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
    public Task<IActionResult> Localizacoes() => ModulePage("georreferenciamento", "Localizações", "campo.localizacao.enviar", "/api/campo/atividades/1/localizacoes");
}

[AllowAnonymous]
public sealed class OfflineController : Controller
{
    public IActionResult Index() => View();
}
