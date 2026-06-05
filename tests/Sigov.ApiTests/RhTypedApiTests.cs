using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class RhTypedApiTests
{
    [Fact]
    public void RhTypedController_Deve_Expor_Rotas_Tipadas_Sem_Remover_Genericas()
    {
        var root = FindRepositoryRoot();
        var typed = File.ReadAllText(Path.Combine(root, "src/Sigov.Api/Controllers/RhTypedController.cs"));
        var generic = File.ReadAllText(Path.Combine(root, "src/Sigov.Api/Controllers/RhController.cs"));

        typed.Should().Contain("servidores-tipado");
        typed.Should().Contain("folhas-tipado/{folhaId:long}/lancamentos");
        typed.Should().Contain("folhas-tipado/integrar-financeiro");
        typed.Should().Contain("portal-tipado/servidores/{servidorId:long}");
        generic.Should().Contain("{recurso}");
        generic.Should().Contain("export/{recurso}.{formato}");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Raiz não encontrada.");
    }
}
