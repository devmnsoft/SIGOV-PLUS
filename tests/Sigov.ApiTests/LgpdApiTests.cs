using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class LgpdApiTests
{
    [Fact]
    public void Lgpd_Deve_Ter_Dashboard_E_Relatorio()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Lgpd/Index.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Lgpd/RelatorioTitular.cshtml")).Should().BeTrue();
    }
}
