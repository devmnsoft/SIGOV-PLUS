using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class IndustriaController : EnterprisePageControllerBase
{
    public IndustriaController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }

    public Task<IActionResult> Dashboard() => IndustriaPage("Dashboard Industrial", "industria.dashboard.visualizar", "/api/industria/dashboard");
    public Task<IActionResult> CentrosTrabalho() => IndustriaPage("Centros de Trabalho", "industria.centros.visualizar", "/api/industria/centros-trabalho");
    public Task<IActionResult> Recursos() => IndustriaPage("Recursos Produtivos", "industria.recursos.visualizar", "/api/industria/recursos");
    public Task<IActionResult> Produtos() => IndustriaPage("Produtos Industriais", "industria.produtos.visualizar", "/api/industria/produtos");
    public Task<IActionResult> FichasTecnicas() => IndustriaPage("Ficha Técnica / BOM", "industria.fichas.visualizar", "/api/industria/fichas-tecnicas");
    public Task<IActionResult> Roteiros() => IndustriaPage("Roteiros de Produção", "industria.roteiros.visualizar", "/api/industria/roteiros");
    public Task<IActionResult> OrdensProducao() => IndustriaPage("Ordens de Produção", "industria.ordens.visualizar", "/api/industria/ordens-producao");
    public Task<IActionResult> Apontamentos() => IndustriaPage("Apontamentos de Produção", "industria.apontamentos.criar", "/api/industria/ordens-producao");
    public Task<IActionResult> Qualidade() => IndustriaPage("Controle de Qualidade", "industria.qualidade.visualizar", "/api/industria/qualidade");
    public Task<IActionResult> Paradas() => IndustriaPage("Paradas Produtivas", "industria.paradas.visualizar", "/api/industria/paradas");
    public Task<IActionResult> Custos() => IndustriaPage("Custos Industriais", "industria.custos.visualizar", "/api/industria/custos");
    public Task<IActionResult> ChaoFabrica() => IndustriaPage("Chão de Fábrica", "industria.chao_fabrica.acessar", "/api/industria/ordens-producao");

    private Task<IActionResult> IndustriaPage(string title, string permission, string apiRoute) => ModulePage("industria_producao", title, permission, apiRoute);
}
