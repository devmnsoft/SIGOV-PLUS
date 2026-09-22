using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class SaasAdminRegressionTests
{
    [Fact]
    public void SaasAdmin_Deve_Ter_Telas_Principais()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml")).Should().Contain("Assinatura").And.Contain("Auditoria");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/Tenants.cshtml")).Should().NotContain("Município Comercial");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml")).Should().NotContain("Município Comercial");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs")).Should().Contain("ISaasTenantAdministrationService").And.Contain("justification");
        var service = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        service.Should().Contain("reativação não renova o contrato automaticamente")
            .And.Contain("Somente uma contratação suspensa pode ser reativada")
            .And.Contain("if (before is null)")
            .And.Contain("var currentContract = before;")
            .And.Contain("PreserveTerm = !isNewContract")
            .And.Contain("tenant_modulo_contratado.vigencia_inicio")
            .And.Contain("RandomNumberGenerator.GetBytes(48)")
            .And.Contain("deve_alterar_senha")
            .And.Contain("tenant_id=@TenantId and (codigo_externo=@Codigo or nome=@Codigo)")
            .And.Contain("RevokeTenantSessionsAsync")
            .And.Contain("RevokeUserSessionsAsync")
            .And.NotContain("$2a$11$")
            .And.NotContain("before!");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml"))
            .Should().Contain("nenhuma cobrança será quitada ou renovada automaticamente")
            .And.Contain("não inclui dependências silenciosamente")
            .And.Contain("o cadastro não gera nem exibe senha provisória")
            .And.Contain("Recuperar acesso");
    }
}
