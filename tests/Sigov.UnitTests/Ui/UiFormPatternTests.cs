using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.UnitTests.Ui;

public sealed class UiFormPatternTests
{
    [Theory]
    [InlineData("src/Sigov.Web/Views/Pessoas/Criar.cshtml")]
    [InlineData("src/Sigov.Web/Views/Pessoas/Editar.cshtml")]
    [InlineData("src/Sigov.Web/Views/Seguranca/Usuarios.cshtml")]
    [InlineData("src/Sigov.Web/Views/Lgpd/Solicitacoes.cshtml")]
    public void Forms_Post_Deve_Ter_Antiforgery(string path)
    {
        File.ReadAllText(TestRepoPath.Get(path)).Should().Contain("@Html.AntiForgeryToken()");
    }
}
