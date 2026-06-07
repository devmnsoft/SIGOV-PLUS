using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class SecurityModuleRegressionTests
{
    [Fact]
    public void Seguranca_Lote1_Views_Deve_Existir()
    {
        Directory.GetFiles(TestRepoPath.Get("src/Sigov.Web/Views/Seguranca"), "*.cshtml").Should().HaveCountGreaterThanOrEqualTo(6);
    }
}
