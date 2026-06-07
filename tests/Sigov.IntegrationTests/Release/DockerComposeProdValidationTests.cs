using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Release;

public sealed class DockerComposeProdValidationTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void DockerComposeProd_Deve_Usar_Rede_Interna_E_Secrets_Por_Env()
    {
        var content = File.ReadAllText(Path.Combine(Root, "docker-compose.prod.yml"));

        content.Should().Contain("sigov-internal");
        content.Should().Contain("internal: true");
        content.Should().Contain("POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-REPLACE_WITH_SECRET}");
        content.Should().NotContain("5432:5432");
        content.Should().Contain("ASPNETCORE_ENVIRONMENT: Production");
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
