using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild05ComercioTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610120000_pos_build_05_comercio_varejo_atacado_pdv_caixa_financeiro.sql"));
    private static readonly string ComercioApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/ComercioController.cs"));
    private static readonly string FinanceiroApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/FinanceiroComercialController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));

    [Fact]
    public void Migration_cria_tabelas_com_tenant_id_indices_permissoes_e_sem_drop()
    {
        Migration.Should().Contain("create table if not exists sigov.comercio_cliente");
        Migration.Should().Contain("tenant_id bigint not null");
        Migration.Should().Contain("create table if not exists sigov.comercio_venda");
        Migration.Should().Contain("create table if not exists sigov.comercio_caixa");
        Migration.Should().Contain("create table if not exists sigov.financeiro_conta_receber");
        Migration.Should().Contain("create index if not exists ix_comercio_venda_status_data");
        Migration.Should().Contain("comercio.estoque.vender_negativo");
        Migration.Should().Contain("financeiro.contas_receber.receber");
        Migration.ToLowerInvariant().Should().NotContain("drop table");
    }

    [Fact]
    public void Api_entrega_rotas_de_clientes_produtos_pedidos_pdv_caixa_com_lgpd_e_auditoria()
    {
        ComercioApi.Should().Contain("api/comercio");
        ComercioApi.Should().Contain("clientes/{id:long}/status");
        ComercioApi.Should().Contain("vendas/{id:long}/finalizar");
        ComercioApi.Should().Contain("caixas/{id:long}/fechar");
        ComercioApi.Should().Contain("pedidos/{id:long}/gerar-os");
        ComercioApi.Should().Contain("right(documento,4)");
        ComercioApi.Should().Contain("CLIENTE_CRIADO");
        ComercioApi.Should().Contain("VENDA_FINALIZADA");
        ComercioApi.Should().Contain("CONTA_RECEBER_GERADA");
    }

    [Fact]
    public void Regras_minimas_de_venda_caixa_estoque_e_financeiro_estao_expressas()
    {
        ComercioApi.Should().Contain("Não é permitido finalizar venda sem item");
        ComercioApi.Should().Contain("Não é permitido finalizar venda sem pagamento total");
        ComercioApi.Should().Contain("PDV exige caixa aberto");
        ComercioApi.Should().Contain("Não é permitido vender produto inativo");
        ComercioApi.Should().Contain("BaixarEstoqueVendaAsync");
        ComercioApi.Should().Contain("EstornarEstoqueVendaAsync");
        FinanceiroApi.Should().Contain("api/financeiro/contas-receber");
        FinanceiroApi.Should().Contain("CONTA_RECEBER_RECEBIDA");
    }

    [Fact]
    public void Catalogo_menu_docs_e_script_expoem_varejo_atacado_pdv_caixa_e_financeiro()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Saas/Modules/ModuleCatalogService.cs")).Should().Contain("COMERCIO_STARTER").And.Contain("ATACADO_PRO").And.Contain("BUSINESS_FULL");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs")).Should().Contain("pdv").And.Contain("caixa");
        Sidebar.Should().Contain("data-module=\"comercio_varejo\"").And.Contain("/Atacado/Separacao").And.Contain("/Financeiro/ContasReceber");
        File.ReadAllText(TestRepoPath.Get("docs/comercio-varejo-atacado.md")).Should().Contain("Fluxo atacado");
        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("http://localhost:8080/Comercio/PDV");
    }
}
