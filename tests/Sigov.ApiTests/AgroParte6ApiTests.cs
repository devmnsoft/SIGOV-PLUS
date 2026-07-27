using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;
public sealed class AgroParte6ApiTests
{
    [Fact] public void Controllers_Definem_Endpoints_Privados_E_Publicos()
    {
        var files = Directory.GetFiles(TestRepoPath.Get("src/Sigov.Api/Controllers"), "Agro*.cs").Select(File.ReadAllText).ToArray();
        string.Join('\n', files).Should().Contain("api/agro/bi/dashboard");
        string.Join('\n', files).Should().Contain("api/agro/relatorios/modelos");
        string.Join('\n', files).Should().Contain("api/publico/agro/{tenantSlug}/datasets");
        string.Join('\n', files).Should().Contain("RequireModule(\"agro\")");
    }
}
