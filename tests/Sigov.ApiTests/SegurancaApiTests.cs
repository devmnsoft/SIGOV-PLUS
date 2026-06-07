using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class SegurancaApiTests
{
    [Fact]
    public void Menu_Deve_Ter_Rotas_De_Seguranca()
    {
        File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml")).Should().Contain("/Seguranca/Usuarios").And.Contain("/Seguranca/Permissoes");
    }
}
