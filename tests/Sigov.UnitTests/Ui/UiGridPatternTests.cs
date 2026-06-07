using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.UnitTests.Ui;

public sealed class UiGridPatternTests
{
    [Theory]
    [InlineData("src/Sigov.Web/Views/Pessoas/Index.cshtml", "pessoas-pagination")]
    [InlineData("src/Sigov.Web/Views/Auditoria/Trilhas.cshtml", "pagination")]
    [InlineData("src/Sigov.Web/Views/SaasAdmin/Tenants.cshtml", "pagination")]
    public void Grids_Do_Lote_1_Deve_Ter_Paginacao(string path, string expected)
    {
        File.ReadAllText(TestRepoPath.Get(path)).Should().Contain(expected);
    }
}
