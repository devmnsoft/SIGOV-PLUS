using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Enterprise;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public abstract class EnterprisePageControllerBase : Controller
{
    private static readonly Guid DevelopmentFallbackTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IEnterpriseModuleService _service;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IEnterpriseTenantMappingService _tenantMappingService;

    protected EnterprisePageControllerBase(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService)
    {
        _service = service;
        _tenantContext = tenantContext;
        _environment = environment;
        _configuration = configuration;
        _tenantMappingService = tenantMappingService;
    }

    protected async Task<IActionResult> ModulePage(string module, string title, string permission, string apiRoute)
    {
        var tenantId = await ResolveTenantIdAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        if (tenantId is null)
        {
            return View("~/Views/Enterprise/ModulePage.cshtml", new EnterprisePageViewModel(module, title, permission, apiRoute, new EnterpriseDashboard(module, 0, 1, new[] { "Tenant não resolvido. Selecione um tenant para operar este módulo." }, Array.Empty<EnterpriseAuditEvent>()), null));
        }

        var dashboard = _service.GetDashboard(module, tenantId.Value);
        return View("~/Views/Enterprise/ModulePage.cshtml", new EnterprisePageViewModel(module, title, permission, apiRoute, dashboard, tenantId));
    }

    private async Task<Guid?> ResolveTenantIdAsync(CancellationToken cancellationToken)
    {
        if (TryReadEnterpriseTenant(User.FindFirst("tenant_id")?.Value, out var claimTenant)) return claimTenant;
        if (long.TryParse(User.FindFirst("tenant_id")?.Value, out var coreTenantFromClaim)) return await _tenantMappingService.ResolveEnterpriseTenantAsync(coreTenantFromClaim, cancellationToken).ConfigureAwait(false);
        if (TryReadEnterpriseTenant(Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var headerTenant)) return headerTenant;
        if (long.TryParse(Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var coreTenantFromHeader)) return await _tenantMappingService.ResolveEnterpriseTenantAsync(coreTenantFromHeader, cancellationToken).ConfigureAwait(false);
        var contextTenant = _tenantContext.Resolve();
        if (contextTenant.TenantId.HasValue) return await _tenantMappingService.ResolveEnterpriseTenantAsync(contextTenant.TenantId.Value, cancellationToken).ConfigureAwait(false);
        return _environment.IsDevelopment() && _configuration.GetValue<bool>("DemoMode:Enabled") ? DevelopmentFallbackTenantId : null;
    }

    private static bool TryReadEnterpriseTenant(string? value, out Guid tenantId)
    {
        if (Guid.TryParse(value, out tenantId) && tenantId != Guid.Empty) return true;
        tenantId = Guid.Empty;
        return false;
    }
}

public sealed record EnterprisePageViewModel(string Module, string Title, string Permission, string ApiRoute, EnterpriseDashboard Dashboard, Guid? TenantId = null);

public sealed class OrdemServicoController : EnterprisePageControllerBase
{
    public OrdemServicoController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("ordem_servico", "Dashboard de Ordem de Serviço", "os.dashboard.visualizar", "/api/os/ordens");
    public Task<IActionResult> Ordens() => ModulePage("ordem_servico", "Ordens de Serviço", "os.ordens.visualizar", "/api/os/ordens");
    public Task<IActionResult> Agenda() => ModulePage("ordem_servico", "Agenda Técnica", "os.agenda.visualizar", "/api/os/ordens");
    public Task<IActionResult> Checklist() => ModulePage("ordem_servico", "Checklist de OS", "os.checklist.visualizar", "/api/os/ordens");
    public Task<IActionResult> Apontamentos() => ModulePage("ordem_servico", "Apontamentos de Horas", "os.apontamentos.visualizar", "/api/os/ordens");
}
public sealed class IndustrialController : EnterprisePageControllerBase
{
    public IndustrialController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("manutencao_industrial", "Dashboard Industrial", "industrial.dashboard.visualizar", "/api/enterprise/manutencao_industrial/dashboard");
    public Task<IActionResult> Ativos() => ModulePage("manutencao_industrial", "Ativos Industriais", "industrial.ativos.visualizar", "/api/industrial/ativos");
    public Task<IActionResult> PlanosManutencao() => ModulePage("manutencao_industrial", "Planos de Manutenção", "industrial.planos.visualizar", "/api/industrial/planos-manutencao");
    public Task<IActionResult> Programadas() => ModulePage("manutencao_industrial", "Manutenções Programadas", "industrial.programadas.visualizar", "/api/industrial/planos-manutencao");
    public Task<IActionResult> Medidores() => ModulePage("manutencao_industrial", "Medidores e Leituras", "industrial.medidores.visualizar", "/api/industrial/medidores");
    public Task<IActionResult> Paradas() => ModulePage("manutencao_industrial", "Paradas e Falhas", "industrial.paradas.visualizar", "/api/industrial/paradas");
}
public sealed class EstoqueController : EnterprisePageControllerBase
{
    public EstoqueController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("estoque_compras", "Dashboard de Estoque", "estoque.dashboard.visualizar", "/api/enterprise/estoque_compras/dashboard");
    public Task<IActionResult> Produtos() => ModulePage("estoque_compras", "Produtos", "estoque.produtos.visualizar", "/api/estoque/produtos");
    public Task<IActionResult> Almoxarifados() => ModulePage("estoque_compras", "Almoxarifados", "estoque.almoxarifados.visualizar", "/api/estoque/almoxarifados");
    public Task<IActionResult> Movimentos() => ModulePage("estoque_compras", "Movimentos", "estoque.movimentos.visualizar", "/api/estoque/produtos");
    public Task<IActionResult> Saldos() => ModulePage("estoque_compras", "Saldos", "estoque.saldos.visualizar", "/api/estoque/saldos");
    public Task<IActionResult> Requisicoes() => ModulePage("estoque_compras", "Requisições", "estoque.requisicoes.visualizar", "/api/estoque/requisicoes");
}
public sealed class ComprasComercialController : EnterprisePageControllerBase
{
    public ComprasComercialController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Fornecedores() => ModulePage("estoque_compras", "Fornecedores", "compras.fornecedores.visualizar", "/api/compras/fornecedores");
    public Task<IActionResult> Pedidos() => ModulePage("estoque_compras", "Pedidos de Compra", "compras.pedidos.visualizar", "/api/compras/pedidos");
}
public sealed class ComercioController : EnterprisePageControllerBase
{
    public ComercioController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("comercial", "Dashboard Comércio", "comercio.dashboard.visualizar", "/api/enterprise/comercial/dashboard");
    public Task<IActionResult> Clientes() => ModulePage("comercial", "Clientes Comércio", "comercio.clientes.visualizar", "/api/comercio/clientes");
    public Task<IActionResult> Produtos() => ModulePage("comercial", "Produtos Comércio", "comercio.produtos.visualizar", "/api/comercio/produtos");
    public Task<IActionResult> Orcamentos() => ModulePage("comercial", "Orçamentos", "comercio.orcamentos.visualizar", "/api/comercio/orcamentos");
    public Task<IActionResult> Pedidos() => ModulePage("comercial", "Pedidos de Venda", "comercio.pedidos.visualizar", "/api/comercio/pedidos");
    public Task<IActionResult> Vendas() => ModulePage("comercial", "Vendas Balcão", "comercio.vendas.criar", "/api/comercio/pedidos");
    public Task<IActionResult> PDV() => ModulePage("pdv", "PDV Web", "comercio.pdv.acessar", "/api/comercio/pedidos");
    public Task<IActionResult> Caixa() => ModulePage("caixa", "Caixa Comercial", "comercio.caixa.abrir", "/api/comercio/pedidos");
    public Task<IActionResult> TabelasPreco() => ModulePage("comercio_atacado", "Tabelas de Preço", "comercio.tabelas.visualizar", "/api/comercio/tabelas-preco");
    public Task<IActionResult> Comissoes() => ModulePage("comercial", "Comissões", "comercio.comissoes.visualizar", "/api/comercial/comissoes");
}
public sealed class VarejoController : EnterprisePageControllerBase
{
    public VarejoController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("comercio_varejo", "Dashboard Varejo", "comercio.dashboard.visualizar", "/api/comercio/pedidos");
    public Task<IActionResult> PDV() => ModulePage("pdv", "PDV Varejo", "comercio.pdv.acessar", "/api/comercio/pedidos");
    public Task<IActionResult> Caixa() => ModulePage("caixa", "Caixa Varejo", "comercio.caixa.abrir", "/api/comercio/pedidos");
    public Task<IActionResult> Vendas() => ModulePage("comercio_varejo", "Vendas Varejo", "comercio.vendas.criar", "/api/comercio/pedidos");
}
public sealed class AtacadoController : EnterprisePageControllerBase
{
    public AtacadoController(IEnterpriseModuleService service, ITenantContextAccessor tenantContext, IWebHostEnvironment environment, IConfiguration configuration, IEnterpriseTenantMappingService tenantMappingService) : base(service, tenantContext, environment, configuration, tenantMappingService) { }
    public Task<IActionResult> Dashboard() => ModulePage("comercio_atacado", "Dashboard Atacado", "comercio.dashboard.visualizar", "/api/comercio/pedidos");
    public Task<IActionResult> Pedidos() => ModulePage("comercio_atacado", "Pedidos Atacado", "comercio.pedidos.visualizar", "/api/comercio/pedidos");
    public Task<IActionResult> Clientes() => ModulePage("comercio_atacado", "Clientes Atacado", "comercio.clientes.visualizar", "/api/comercio/clientes");
    public Task<IActionResult> TabelasPreco() => ModulePage("comercio_atacado", "Tabelas de Preço Atacado", "comercio.tabelas.visualizar", "/api/comercio/tabelas-preco");
    public Task<IActionResult> Separacao() => ModulePage("comercio_atacado", "Separação e Conferência", "comercio.pedidos.confirmar", "/api/comercio/pedidos");
}
