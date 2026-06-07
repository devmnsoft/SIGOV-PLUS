using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Release;

public sealed class ProductionConfigValidationTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void AppsettingsProduction_Deve_Manter_Swagger_E_Seed_Desabilitados()
    {
        var content = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "appsettings.Production.json"));

        content.Should().Contain("\"SwaggerEnabledInProduction\": false");
        content.Should().Contain("\"Demo\": false");
        content.Should().Contain("\"Schema\": \"sigov\"");
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
