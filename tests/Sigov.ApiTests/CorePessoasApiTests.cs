using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class CorePessoasApiTests
{
    [Fact]
    public void Pessoas_Api_Deve_Preservar_Rotas_Rest()
    {
        var source = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/PessoasController.cs"));
        source.Should().Contain("[HttpGet]").And.Contain("[HttpPost]").And.Contain("[HttpPut").And.Contain("[HttpDelete");
    }
}
