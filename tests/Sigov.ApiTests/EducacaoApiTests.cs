using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class EducacaoApiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Endpoints_Educacao_Estao_Registrados_E_Protegidos_Por_Service_Guard()
    {
        var api = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs"));
        api.Should().Contain("api/educacao/alunos").And.Contain("api/educacao/matriculas").And.Contain("api/educacao/export");
        var service = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Educacao/EducacaoServices.cs"));
        service.Should().Contain("GuardAsync").And.Contain("HasPermissionAsync").And.Contain("Módulo educação não contratado/habilitado");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "sigov.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
