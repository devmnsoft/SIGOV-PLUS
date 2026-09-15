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
            .And.Contain("PreserveTerm = !isNewContract")
            .And.Contain("tenant_modulo_contratado.vigencia_inicio");
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/SaasAdmin/TenantDetalhe.cshtml"))
            .Should().Contain("nenhuma cobrança será quitada ou renovada automaticamente")
            .And.Contain("não inclui dependências silenciosamente");
    }
}
