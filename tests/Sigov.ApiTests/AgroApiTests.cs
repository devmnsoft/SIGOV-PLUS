using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class AgroApiTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Agro_Dashboard_Deve_Estar_Protegido()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Controllers", "AgroDashboardController.cs"));
        source.Should().Contain("[Authorize]");
        source.Should().Contain("[RequireModule(\"agro\")]");
        source.Should().Contain("api/agro/dashboard");
    }

    [Fact]
    public void Agro_Geo_Deve_Expor_Crud_E_Exportacao_GeoJson()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Controllers", "AgroGeoController.cs"));
        source.Should().Contain("camadas/{id:long}");
        source.Should().Contain("feicoes/{id:long}");
        source.Should().Contain("export.geojson");
        source.Should().Contain("Forbid");
        source.Should().Contain("Unauthorized");
    }

    [Fact]
    public void Agro_Service_Deve_Validar_Permissoes_E_Tenant_Isolation()
    {
        var service = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Agro", "Geo", "AgroGeoService.cs"));
        var repository = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Agro", "AgroModuleRepository.cs"));
        service.Should().Contain("IAgroAccessChecker");
        service.Should().Contain("AgroPermissions.GeoVisualizar");
        var accessChecker = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Agro", "Permissions", "AgroAccessChecker.cs"));
        accessChecker.Should().Contain("CheckFeatureAsync");
        accessChecker.Should().Contain("EffectivePermissionService");
        repository = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Agro", "AgroGeoRepository.cs"));
        repository.Should().Contain("where tenant_id=@TenantId");
        repository.Should().Contain("sigov.agro_geo_camada");
        repository.Should().Contain("sigov.agro_geo_feicao");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório sigov não encontrada.");
    }
}
