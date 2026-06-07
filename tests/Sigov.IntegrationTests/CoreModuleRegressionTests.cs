using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.IntegrationTests;

public sealed class CoreModuleRegressionTests
{
    [Fact]
    public void Core_Lote1_Views_Deve_Existir()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Pessoas/Index.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Core/Entidades.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Core/Exercicios.cshtml")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Core/Unidades.cshtml")).Should().BeTrue();
    }
}
