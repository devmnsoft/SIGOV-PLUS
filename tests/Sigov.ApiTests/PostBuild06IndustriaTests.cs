using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public class PostBuild06IndustriaTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610150000_pos_build_06_industria_producao.sql"));
    private static readonly string EvolutionMigration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260908120000_evolucao_saas_industria_360.sql"));
    private static readonly string MaintenanceIntegrationMigration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260914120000_corr_industria_manutencao_integracao.sql"));
    private static readonly string OperationalFlowMigration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260915120000_industria_fluxo_operacional.sql"));
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
    public void Apontamento_DeveSerIdempotenteConcorrenteERastreavel()
    {
        IndustriaApi.Should().Contain("Idempotency-Key");
        IndustriaApi.Should().Contain("for update");
        IndustriaApi.Should().Contain("quantidade_produzida + r.QuantidadeBoas > ordem.QuantidadePlanejada");
        IndustriaApi.Should().Contain("A quantidade produzida é consolidada exclusivamente pelo apontamento");
        OperationalFlowMigration.Should().Contain("ux_industria_apontamento_idempotencia");
        OperationalFlowMigration.Should().Contain("apontamento_id bigint references sigov.industria_apontamento(id)");
        OperationalFlowMigration.Should().Contain("quantidade_aprovada numeric(14,4)");
    }

    [Fact]
    public void ParadaIndustrial_DeveAbrirManutencaoReal_IdempotenteETransacional()
    {
        IndustriaApi.Should().Contain("insert into sigov.manutencao_ordem_servico");
        IndustriaApi.Should().Contain("BeginTransaction(System.Data.IsolationLevel.ReadCommitted)");
        IndustriaApi.Should().Contain("on conflict(tenant_id,origem_tipo,origem_id)");
        IndustriaApi.Should().Contain("for update of p");
        IndustriaApi.Should().NotContain("nextval(pg_get_serial_sequence('sigov.industria_ordem_producao','id'))");
        MaintenanceIntegrationMigration.Should().Contain("create unique index if not exists ux_manutencao_os_origem");
        MaintenanceIntegrationMigration.Should().Contain("check ((origem_tipo is null) = (origem_id is null))");
    }

    [Fact]
    public void ParadaIndustrial_DeveValidarContextoNullableSemInferirIdentidade()
    {
        IndustriaApi.Should().Contain("_tenant.EntidadeId is not long entidadeId || _user.UsuarioId is not long usuarioId");
        IndustriaApi.Should().Contain("Entidade e usuário do contexto são obrigatórios");
        IndustriaApi.Should().Contain("usuarioId, \"ordem_servico\"");
        IndustriaApi.Should().Contain("EntidadeId = entidadeId");
        IndustriaApi.Should().Contain("UsuarioId = usuarioId");
        IndustriaApi.Should().NotContain("_tenant.EntidadeId.Value");
        IndustriaApi.Should().NotContain("_user.UsuarioId.Value");
        IndustriaApi.Should().NotContain("_user.UsuarioId ?? 0");
    }

    [Fact]
    public void Web_DeveConterMenuETelasIndustriais()
    {
        Sidebar.Should().Contain("/Industria/Dashboard");
        Sidebar.Should().Contain("/Industria/ChaoFabrica");
        Sidebar.Should().Contain("IRequestAuthorizationSnapshot");
        Sidebar.Should().Contain("hasModule(\"industria_producao\")");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Industria/IndustriaIntegracaoServices.cs"))
            .Should().NotContain("from sigov.tenant_modulo_contratado where tenant_id=@TenantId and modulo_codigo='industria_producao'");
        IndustriaApi.Should().Contain("IModuleEntitlementEvaluator");
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
