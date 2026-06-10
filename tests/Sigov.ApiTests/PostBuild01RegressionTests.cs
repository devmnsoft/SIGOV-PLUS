using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild01RegressionTests
{
    [Fact]
    public void PosBuild01_Deve_Publicar_Rotas_Web_Principais()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Controllers/AuthController.cs")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Controllers/DashboardController.cs")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Saas/Tenants.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Saas/Modulos.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Operacao/Health.cshtml")).Should().BeTrue();
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Seed_Admin_E_Auditoria_Idempotente()
    {
        var migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260609090000_pos_build_dashboard_saas.sql"));
        migration.Should().Contain("create table if not exists sigov.auditoria_evento");
        migration.Should().Contain("admin@sigov.local");
        migration.Should().Contain("SIGOV_PBKDF2_V1");
        migration.Should().Contain("on conflict");
        migration.ToLowerInvariant().Should().NotContain("drop table");
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Apis_Saas_E_Health_Visual()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/SaasTenantsController.cs")).Should().Contain("api/saas/tenants");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/SaasModulesController.cs")).Should().Contain("/ativar").And.Contain("/desativar");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/OperacaoHealthController.cs")).Should().Contain("api/operacao/health");
    }

    [Fact]
    public void PosBuild01_Deve_Ter_Documentacao_E_Scripts_De_Ambiente_Local()
    {
        File.Exists(TestRepoPath.Get("docs/ambiente-local.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("scripts/check-local.ps1")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("scripts/demo-local.ps1")).Should().BeTrue();
    }
}
