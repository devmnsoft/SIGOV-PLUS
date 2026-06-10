using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class PostBuild03EvolutionTests
{
    private static readonly string Migration = File.ReadAllText(Path.Combine(TestRepoPath.Root, "database/postgres/migrations/20260609120000_pos_build_03_saas_implantacao_tributario.sql"));
    private static string Api(string relative) => File.ReadAllText(Path.Combine(TestRepoPath.Root, relative));

    [Fact]
    public void Migration_cria_planos_implantacao_parametros_e_tributario_no_schema_sigov()
    {
        Migration.Should().Contain("sigov.saas_implantacao");
        Migration.Should().Contain("sigov.saas_assinatura_historico");
        Migration.Should().Contain("sigov.tenant_parametro");
        Migration.Should().Contain("sigov.tributario_configuracao");
        Migration.Should().Contain("sigov.tributario_contribuinte");
        Migration.Should().Contain("create table if not exists");
        Migration.Should().Contain("on conflict");
    }

    [Fact]
    public void Seeds_incluem_planos_comerciais_e_modulo_tributario()
    {
        Migration.Should().Contain("STARTER");
        Migration.Should().Contain("GOV_BASIC");
        Migration.Should().Contain("GOV_PLUS");
        Migration.Should().Contain("ENTERPRISE");
        Migration.Should().Contain("tributario");
    }

    [Fact]
    public void Api_tributario_exige_modulo_e_mascara_dados_pessoais()
    {
        var controller = Api("src/Sigov.Api/Controllers/TributarioController.cs");
        controller.Should().Contain("[RequireModule(\"tributario\")]");
        controller.Should().Contain("right(documento,4)");
        controller.Should().Contain("tenant_id=@TenantId");
    }

    [Fact]
    public void Api_assinatura_bloqueia_duas_ativas_por_indice_e_registra_historico()
    {
        Migration.Should().Contain("ux_saas_assinatura_ativa_tenant");
        var controller = Api("src/Sigov.Api/Controllers/SaasTenantComercialController.cs");
        controller.Should().Contain("saas_assinatura_historico");
        controller.Should().Contain("ASSINATURA_UPGRADE");
        controller.Should().Contain("ASSINATURA_DOWNGRADE");
    }

    [Fact]
    public void Web_expõe_telas_saas_e_tributario_base()
    {
        Api("src/Sigov.Web/Controllers/SaasController.cs").Should().Contain("Planos").And.Contain("Implantacao").And.Contain("Parametros");
        Api("src/Sigov.Web/Controllers/TributarioController.cs").Should().Contain("Configuracao").And.Contain("TiposCadastro").And.Contain("CamposDinamicos");
    }
}
