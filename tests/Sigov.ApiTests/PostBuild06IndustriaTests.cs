using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public class PostBuild06IndustriaTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610150000_pos_build_06_industria_producao.sql"));
    private static readonly string EvolutionMigration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260908120000_evolucao_saas_industria_360.sql"));
    private static readonly string IndustriaApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/IndustriaController.cs"));
    private static readonly string ComercialIntegracaoApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/IndustriaComercialController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));
    private static readonly string ModulePage = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Industria/ModulePage.cshtml"));

    [Fact]
    public void Migration_DeveCriarTabelasIndustriaisComTenantId()
    {
        Migration.Should().Contain("create table if not exists sigov.industria_centro_trabalho");
        Migration.Should().Contain("create table if not exists sigov.industria_ordem_producao");
        Migration.Should().Contain("create table if not exists sigov.industria_apontamento");
        Migration.Should().Contain("tenant_id bigint not null");
        Migration.Should().Contain("create index if not exists ix_industria_op_status");
    }

    [Fact]
    public void Saas_DeveConterModuloPacotesEPermissoesIndustriais()
    {
        Migration.Should().Contain("industria_producao");
        Migration.Should().Contain("INDUSTRIAL_STARTER");
        Migration.Should().Contain("INDUSTRIAL_PLUS");
        Migration.Should().Contain("FACTORY_FULL");
        Migration.Should().Contain("industria.chao_fabrica.acessar");
        Migration.Should().Contain("GERENTE_INDUSTRIAL");
        EvolutionMigration.Should().Contain("tenant_modulo_contratado_historico");
        EvolutionMigration.Should().Contain("Indústria 360");
        EvolutionMigration.Should().Contain("valor_contratado numeric(18,2)");
    }

    [Fact]
    public void Api_E_Tela_Industriais_Devem_Ser_FailClosed_E_Sem_Dados_Demo()
    {
        IndustriaApi.Should().Contain("IAuthorizationEvaluator");
        IndustriaApi.Should().Contain("ReadPermission");
        IndustriaApi.Should().NotContain("=> User.Identity?.IsAuthenticated != true ||");
        ComercialIntegracaoApi.Should().NotContain("=> User.Identity?.IsAuthenticated != true ||");
        ModulePage.Should().Contain("data-industria-page");
        ModulePage.Should().NotContain(">DEMO<");
    }

    [Fact]
    public void Api_DeveExporEndpointsIndustriaisEIntegracoes()
    {
        IndustriaApi.Should().Contain("api/industria");
        IndustriaApi.Should().Contain("centros-trabalho");
        IndustriaApi.Should().Contain("ordens-producao/{id:long}/consumir-material");
        IndustriaApi.Should().Contain("ordens-producao/{id:long}/calcular-custos");
        IndustriaApi.Should().Contain("paradas/{id:long}/gerar-os");
        ComercialIntegracaoApi.Should().Contain("pedidos/{id:long}/gerar-op");
    }

    [Fact]
    public void Web_DeveConterMenuETelasIndustriais()
    {
        Sidebar.Should().Contain("/Industria/Dashboard");
        Sidebar.Should().Contain("/Industria/ChaoFabrica");
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Industria/Dashboard.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Industria/OrdensProducao.cshtml")).Should().BeTrue();
    }

    [Fact]
    public void DocumentacaoEScriptCompletoDevemExistir()
    {
        File.Exists(TestRepoPath.Get("database/script_completo.sql")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/industria-producao.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/integracao-industria-comercial-estoque-os.md")).Should().BeTrue();
        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("http://localhost:8080/Industria/Custos");
    }
}
