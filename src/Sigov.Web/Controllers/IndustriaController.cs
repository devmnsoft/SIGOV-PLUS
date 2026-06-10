using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class IndustriaController : EnterprisePageControllerBase
{
    public IndustriaController(IEnterpriseModuleService service) : base(service) { }

    public IActionResult Dashboard() => IndustriaPage("Dashboard Industrial", "industria.dashboard.visualizar", "/api/industria/dashboard");
    public IActionResult CentrosTrabalho() => IndustriaPage("Centros de Trabalho", "industria.centros.visualizar", "/api/industria/centros-trabalho");
    public IActionResult Recursos() => IndustriaPage("Recursos Produtivos", "industria.recursos.visualizar", "/api/industria/recursos");
    public IActionResult Produtos() => IndustriaPage("Produtos Industriais", "industria.produtos.visualizar", "/api/industria/produtos");
    public IActionResult FichasTecnicas() => IndustriaPage("Ficha Técnica / BOM", "industria.fichas.visualizar", "/api/industria/fichas-tecnicas");
    public IActionResult Roteiros() => IndustriaPage("Roteiros de Produção", "industria.roteiros.visualizar", "/api/industria/roteiros");
    public IActionResult OrdensProducao() => IndustriaPage("Ordens de Produção", "industria.ordens.visualizar", "/api/industria/ordens-producao");
    public IActionResult Apontamentos() => IndustriaPage("Apontamentos de Produção", "industria.apontamentos.criar", "/api/industria/ordens-producao/{id}/apontamentos");
    public IActionResult Qualidade() => IndustriaPage("Controle de Qualidade", "industria.qualidade.visualizar", "/api/industria/qualidade/inspecoes");
    public IActionResult Paradas() => IndustriaPage("Paradas Produtivas", "industria.paradas.visualizar", "/api/industria/paradas");
    public IActionResult Custos() => IndustriaPage("Custos Industriais", "industria.custos.visualizar", "/api/industria/ordens-producao/{id}/custos");
    public IActionResult ChaoFabrica() => IndustriaPage("Chão de Fábrica", "industria.chao_fabrica.acessar", "/api/industria/ordens-producao");

    private IActionResult IndustriaPage(string title, string permission, string apiRoute) => ModulePage("industria_producao", title, permission, apiRoute);
}
