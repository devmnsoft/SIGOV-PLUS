using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;
public sealed class AgroParte6ApiTests
{
    [Fact] public void Controllers_Definem_Endpoints_Privados_E_Publicos()
    {
        var files = Directory.GetFiles(TestRepoPath.Get("src/Sigov.Api/Controllers"), "Agro*.cs").Select(File.ReadAllText).ToArray();
        var source = string.Join('\n', files);
        source.Should().Contain("Route(\"api/agro/bi\")").And.Contain("HttpGet(\"dashboard\")");
        source.Should().Contain("api/agro/relatorios/modelos");
        source.Should().Contain("Route(\"api/publico/agro/{tenantSlug}\")").And.Contain("HttpGet(\"datasets\")");
        source.Should().Contain("RequireModule(\"agro\")");
    }
}
