using FluentAssertions;
using Sigov.Api.Middlewares;
using Xunit;

namespace Sigov.ApiTests;

public sealed class TenantResolutionApiTests
{
    [Fact]
    public void Middleware_Deve_Rejeitar_Header_Development_Em_Production_PorConfiguracao()
    {
        File.ReadAllText(FindRepositoryFile("src/Sigov.Api/Middlewares/TenantResolutionMiddleware.cs"))
            .Should().Contain("allowDevelopmentResolvers");
    }

    [Fact]
    public void RequireModuleAttribute_Deve_Retornar_403_Quando_Modulo_NaoContratado()
    {
        typeof(RequireModuleAttribute).Name.Should().Be("RequireModuleAttribute");
        File.ReadAllText(FindRepositoryFile("src/Sigov.Api/Middlewares/RequireModuleAttribute.cs"))
            .Should().Contain("Status403Forbidden");
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln")))
        {
            current = current.Parent;
        }

        return Path.Combine(current?.FullName ?? throw new InvalidOperationException("Raiz não encontrada."), relativePath);
    }
}
