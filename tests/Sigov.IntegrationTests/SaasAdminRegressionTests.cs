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
    }
}
