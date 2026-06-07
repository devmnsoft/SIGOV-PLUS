using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Ui;

public sealed class UserPreferencesRepositoryTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Repositorio_Deve_Usar_Schema_Sigov_E_Filtro_Tenant_Usuario()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Ui", "UserPreferenceRepository.cs"));

        source.Should().Contain("sigov.usuario_preferencia");
        source.Should().Contain("tenant_id");
        source.Should().Contain("usuario_id");
        source.Should().NotContain("dbo.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
