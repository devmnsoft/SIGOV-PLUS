using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class PostBuild11IaTests
{
    [Fact]
    public void ScriptCompleto_DeveConterBaseIaAuditavelPorTenant()
    {
        var sql = File.ReadAllText(TestRepoPath.Get("database/script_completo.sql"));
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260611110000_pos_build_11_ia_automacao_assistentes.sql"));

        sql.Should().Contain("sigov.ia_configuracao_tenant");
        sql.Should().Contain("sigov.ia_execucao");
        sql.Should().Contain("tenant_id bigint not null");
        sql.Should().Contain("correlation_id uuid not null");
        sql.Should().Contain("sigov.ia_consumo");
        sql.Should().Contain("on conflict(tenant_id,competencia)");
        migration.Should().Contain("sigov.ia_feedback_usuario");
    }

    [Fact]
    public void Catalogo_DeveConterModulosAddonsPacotesEPermissoesIa()
    {
        var catalogo = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"));
        var sql = File.ReadAllText(TestRepoPath.Get("database/script_completo.sql"));

        catalogo.Should().Contain("ia_assistente");
        catalogo.Should().Contain("ia_documental");
        catalogo.Should().Contain("AI_ENTERPRISE");
        sql.Should().Contain("ia_1000_interacoes");
        sql.Should().Contain("ia_automacoes_avancadas");
        sql.Should().Contain("ia_interacoes");
        sql.Should().Contain("ia.configuracao.editar");
    }

    [Fact]
    public void Api_DeveExporEndpointsIaComProviderInternoEMascaramento()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/IaController.cs"));
        var services = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Ia/IaServices.cs"));

        controller.Should().Contain("Route(\"api/ia\")");
        controller.Should().Contain("HttpPost(\"executar\")");
        controller.Should().Contain("HttpPost(\"documentos/{documentoId:long}/classificar\")");
        controller.Should().Contain("HttpPost(\"predicoes/inadimplencia\")");
        services.Should().Contain("InternalIaProviderClient");
        services.Should().Contain("MaskSensitiveData");
        services.Should().Contain("CPF_MASCARADO");
    }

    [Fact]
    public void Web_DeveConterTelasIaEMenuDinamico()
    {
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/IAController.cs"));
        var sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));
        var page = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/IA/IaPage.cshtml"));

        controller.Should().Contain("Dashboard");
        controller.Should().Contain("Assistente");
        controller.Should().Contain("Consumo");
        sidebar.Should().Contain("/IA/Dashboard");
        sidebar.Should().Contain("data-module=\"ia_predicoes\"");
        page.Should().Contain("spinner-border");
    }
}
