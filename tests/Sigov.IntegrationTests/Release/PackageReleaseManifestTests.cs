using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Release;

public sealed class PackageReleaseManifestTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void PackageReleaseScript_Nao_Deve_Incluir_Padroes_De_Secrets_No_Manifest()
    {
        var script = File.ReadAllText(Path.Combine(Root, "scripts", "package-release.ps1"));

        script.Should().Contain("release-manifest.json");
        script.Should().Contain("checksums");
        script.Should().Contain("Item inseguro no pacote");
    }


    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
