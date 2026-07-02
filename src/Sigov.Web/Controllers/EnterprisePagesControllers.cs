using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;

namespace Sigov.Web.Controllers;

[Authorize]
public abstract class EnterprisePageControllerBase : Controller
{
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IEnterpriseModuleService _service;

    protected EnterprisePageControllerBase(IEnterpriseModuleService service) => _service = service;

    protected IActionResult ModulePage(string module, string title, string permission, string apiRoute)
    {
        var dashboard = _service.GetDashboard(module, DemoTenantId);
        return View("~/Views/Enterprise/ModulePage.cshtml", new EnterprisePageViewModel(module, title, permission, apiRoute, dashboard));
    }
}

public sealed record EnterprisePageViewModel(string Module, string Title, string Permission, string ApiRoute, EnterpriseDashboard Dashboard);

public sealed class OrdemServicoController : EnterprisePageControllerBase
{
    public OrdemServicoController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("ordem_servico", "Dashboard de Ordem de Serviço", "os.dashboard.visualizar", "/api/os/ordens");
    public IActionResult Ordens() => ModulePage("ordem_servico", "Ordens de Serviço", "os.ordens.visualizar", "/api/os/ordens");
    public IActionResult Agenda() => ModulePage("ordem_servico", "Agenda Técnica", "os.agenda.visualizar", "/api/os/ordens/{id}/agendar");
    public IActionResult Checklist() => ModulePage("ordem_servico", "Checklist de OS", "os.checklist.visualizar", "/api/os/ordens/{id}/checklist");
    public IActionResult Apontamentos() => ModulePage("ordem_servico", "Apontamentos de Horas", "os.apontamentos.visualizar", "/api/os/ordens/{id}/apontamentos");
}

public sealed class IndustrialController : EnterprisePageControllerBase
{
    public IndustrialController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("manutencao_industrial", "Dashboard Industrial", "industrial.dashboard.visualizar", "/api/enterprise/manutencao_industrial/dashboard");
    public IActionResult Ativos() => ModulePage("manutencao_industrial", "Ativos Industriais", "industrial.ativos.visualizar", "/api/industrial/ativos");
    public IActionResult PlanosManutencao() => ModulePage("manutencao_industrial", "Planos de Manutenção", "industrial.planos.visualizar", "/api/industrial/planos-manutencao");
    public IActionResult Programadas() => ModulePage("manutencao_industrial", "Manutenções Programadas", "industrial.programadas.visualizar", "/api/industrial/planos-manutencao/{id}/gerar-os");
    public IActionResult Medidores() => ModulePage("manutencao_industrial", "Medidores e Leituras", "industrial.medidores.visualizar", "/api/industrial/medidores");
    public IActionResult Paradas() => ModulePage("manutencao_industrial", "Paradas e Falhas", "industrial.paradas.visualizar", "/api/industrial/paradas");
}

public sealed class EstoqueController : EnterprisePageControllerBase
{
    public EstoqueController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("estoque_compras", "Dashboard de Estoque", "estoque.dashboard.visualizar", "/api/enterprise/estoque_compras/dashboard");
    public IActionResult Produtos() => ModulePage("estoque_compras", "Produtos", "estoque.produtos.visualizar", "/api/estoque/produtos");
    public IActionResult Almoxarifados() => ModulePage("estoque_compras", "Almoxarifados", "estoque.almoxarifados.visualizar", "/api/estoque/almoxarifados");
    public IActionResult Movimentos() => ModulePage("estoque_compras", "Movimentos", "estoque.movimentos.visualizar", "/api/estoque/movimentos/entrada");
    public IActionResult Saldos() => ModulePage("estoque_compras", "Saldos", "estoque.saldos.visualizar", "/api/estoque/saldos");
    public IActionResult Requisicoes() => ModulePage("estoque_compras", "Requisições", "estoque.requisicoes.visualizar", "/api/estoque/requisicoes");
}

public sealed class ComprasComercialController : EnterprisePageControllerBase
{
    public ComprasComercialController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Fornecedores() => ModulePage("estoque_compras", "Fornecedores", "compras.fornecedores.visualizar", "/api/compras/fornecedores");
    public IActionResult Pedidos() => ModulePage("estoque_compras", "Pedidos de Compra", "compras.pedidos.visualizar", "/api/compras/pedidos");
}

public sealed class ComercioController : EnterprisePageControllerBase
{
    public ComercioController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("comercial", "Dashboard Comércio", "comercio.dashboard.visualizar", "/api/comercio/vendas");
    public IActionResult Clientes() => ModulePage("comercial", "Clientes Comércio", "comercio.clientes.visualizar", "/api/comercio/clientes");
    public IActionResult Produtos() => ModulePage("comercial", "Produtos Comércio", "comercio.produtos.visualizar", "/api/comercio/produtos");
    public IActionResult Orcamentos() => ModulePage("comercial", "Orçamentos", "comercio.orcamentos.visualizar", "/api/comercio/orcamentos");
    public IActionResult Pedidos() => ModulePage("comercial", "Pedidos de Venda", "comercio.pedidos.visualizar", "/api/comercio/pedidos");
    public IActionResult Vendas() => ModulePage("comercial", "Vendas Balcão", "comercio.vendas.criar", "/api/comercio/vendas");
    public IActionResult PDV() => ModulePage("pdv", "PDV Web", "comercio.pdv.acessar", "/api/comercio/vendas");
    public IActionResult Caixa() => ModulePage("caixa", "Caixa Comercial", "comercio.caixa.abrir", "/api/comercio/caixas");
    public IActionResult TabelasPreco() => ModulePage("comercio_atacado", "Tabelas de Preço", "comercio.tabelas.visualizar", "/api/comercio/tabelas-preco");
    public IActionResult Comissoes() => ModulePage("comercial", "Comissões", "comercio.comissoes.visualizar", "/api/comercio/comissoes");
}

public sealed class VarejoController : EnterprisePageControllerBase
{
    public VarejoController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("comercio_varejo", "Dashboard Varejo", "comercio.dashboard.visualizar", "/api/comercio/vendas");
    public IActionResult PDV() => ModulePage("pdv", "PDV Varejo", "comercio.pdv.acessar", "/api/comercio/vendas");
    public IActionResult Caixa() => ModulePage("caixa", "Caixa Varejo", "comercio.caixa.abrir", "/api/comercio/caixas");
    public IActionResult Vendas() => ModulePage("comercio_varejo", "Vendas Varejo", "comercio.vendas.criar", "/api/comercio/vendas");
}

public sealed class AtacadoController : EnterprisePageControllerBase
{
    public AtacadoController(IEnterpriseModuleService service) : base(service) { }
    public IActionResult Dashboard() => ModulePage("comercio_atacado", "Dashboard Atacado", "comercio.dashboard.visualizar", "/api/comercio/pedidos");
    public IActionResult Pedidos() => ModulePage("comercio_atacado", "Pedidos Atacado", "comercio.pedidos.visualizar", "/api/comercio/pedidos");
    public IActionResult Clientes() => ModulePage("comercio_atacado", "Clientes Atacado", "comercio.clientes.visualizar", "/api/comercio/clientes");
    public IActionResult TabelasPreco() => ModulePage("comercio_atacado", "Tabelas de Preço Atacado", "comercio.tabelas.visualizar", "/api/comercio/tabelas-preco");
    public IActionResult Separacao() => ModulePage("comercio_atacado", "Separação e Conferência", "comercio.pedidos.confirmar", "/api/comercio/pedidos/{id}/separar");
}
