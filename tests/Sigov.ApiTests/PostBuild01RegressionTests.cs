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
        migration.Should().Contain("baseline estrutural não cria usuário, e-mail ou senha administrativa padrão");
        migration.Should().NotContain("admin@sigov.local");
        migration.Should().NotContain("SIGOV_PBKDF2_V1");
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

    [Fact]
    public void Recebimento_Deve_Revalidar_Idempotencia_Depois_Do_Lock_Do_Pedido()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        var methodStart = repository.IndexOf("public async Task<RecebimentoCriado> CriarEConfirmarAsync", StringComparison.Ordinal);
        methodStart.Should().BeGreaterThanOrEqualTo(0);

        var methodEnd = repository.IndexOf("private static string? Clean", methodStart, StringComparison.Ordinal);
        methodEnd.Should().BeGreaterThan(methodStart);

        var method = repository[methodStart..methodEnd];
        var lockPosition = method.IndexOf("for update", StringComparison.OrdinalIgnoreCase);
        var queryPositions = System.Text.RegularExpressions.Regex.Matches(method, "new CommandDefinition\\(idempotencyQuery,idempotencyArgs")
            .Select(match => match.Index)
            .ToArray();

        lockPosition.Should().BeGreaterThanOrEqualTo(0);
        queryPositions.Should().HaveCount(2);
        queryPositions[0].Should().BeLessThan(lockPosition);
        queryPositions[1].Should().BeGreaterThan(lockPosition);
    }

    [Fact]
    public void Conferencia_Deve_Ser_Atomica_E_Integrada_A_Central()
    {
        var repository = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/ComprasEmpresariais/ComprasRepositories.cs"));
        var controller = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/ComprasEmpresariaisController.cs"));
        var detail = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/ComprasEmpresariais/Recebimentos/Detalhe.cshtml"));

        repository.Should().Contain("public async Task<RecebimentoCriado> ConcluirInspecaoAsync")
            .And.Contain("for update")
            .And.Contain("INSPECAO_RECEBIMENTO_COMPRA")
            .And.Contain("pendencia_operacional")
            .And.Contain("status='RESOLVIDA'");
        controller.Should().Contain("compras_empresariais.recebimentos.inspecionar")
            .And.Contain("ExportarRecebimentos");
        detail.Should().Contain("Para que serve")
            .And.Contain("Como funciona")
            .And.Contain("Regras importantes")
            .And.Contain("Próximo passo");
    }
}
