using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild07FinanceiroIntegradoTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610180000_pos_build_07_financeiro_integrado.sql"));
    private static readonly string Api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/FinanceiroControllers.cs")) + File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/FinanceiroComercialController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));

    [Fact]
    public void Migration_cria_tabelas_financeiras_com_tenant_id_e_idempotencia()
    {
        foreach (var table in new[] { "financeiro_plano_conta", "financeiro_centro_custo", "financeiro_natureza", "financeiro_conta_bancaria", "financeiro_forma_pagamento", "financeiro_conta_receber", "financeiro_conta_pagar", "financeiro_movimento", "financeiro_baixa_receber", "financeiro_baixa_pagar", "financeiro_conciliacao", "financeiro_rateio", "financeiro_fluxo_caixa_snapshot", "financeiro_configuracao" })
        {
            Migration.Should().Contain($"create table if not exists sigov.{table}");
        }

        Migration.Should().Contain("tenant_id bigint not null").And.Contain("create index if not exists").And.Contain("on conflict");
    }

    [Fact]
    public void Catalogo_e_permissoes_financeiras_estao_seedados()
    {
        var catalog = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"));
        catalog.Should().Contain("financeiro_empresarial").And.Contain("financeiro_publico").And.Contain("BUSINESS_FINANCE").And.Contain("GOV_PLUS");
        Migration.Should().Contain("financeiro.contas_receber.baixar").And.Contain("financeiro.contas_pagar.estornar").And.Contain("financeiro.conciliacao.concluir");
    }

    [Fact]
    public void Apis_financeiras_empresariais_exigem_modulo_e_expoem_fluxos_principais()
    {
        Api.Should().Contain("RequireModule(\"financeiro_empresarial\")");
        foreach (var route in new[] { "api/financeiro/centros-custo", "api/financeiro/contas-bancarias", "api/financeiro/formas-pagamento", "api/financeiro/contas-receber", "api/financeiro/contas-pagar", "api/financeiro/movimentos", "api/financeiro/fluxo-caixa", "api/financeiro/conciliacoes" })
        {
            Api.Should().Contain(route);
        }

        Api.Should().Contain("ILogger").And.Contain("try").And.Contain("catch").And.Contain("correlationId");
    }

    [Fact]
    public void Telas_menu_docs_e_demo_foram_entregues()
    {
        Sidebar.Should().Contain("/Financeiro/ContasPagar").And.Contain("/Financeiro/FluxoCaixa").And.Contain("data-module=\"financeiro_empresarial\"");
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Financeiro/FinanceiroEmpresarial.cshtml")).Should().BeTrue();
        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("http://localhost:8080/Financeiro/Conciliacao");
        File.Exists(TestRepoPath.Get("docs/financeiro-integrado.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/conciliacao-bancaria.md")).Should().BeTrue();
    }
}
