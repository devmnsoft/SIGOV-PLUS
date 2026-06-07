using FluentAssertions;
using Xunit;

namespace Sigov.ApiTests;

public sealed class ModuleAccessRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void RequireModule_E_RequireFeature_Devem_Bloquear_Acesso_Nao_Contratado()
    {
        var moduleAttribute = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Middlewares", "RequireModuleAttribute.cs"));
        var featureAttribute = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Api", "Middlewares", "RequireFeatureAttribute.cs"));

        moduleAttribute.Should().Contain("StatusCodes.Status403Forbidden");
        moduleAttribute.Should().Contain("IsModuleEnabledAsync");
        featureAttribute.Should().Contain("StatusCodes.Status403Forbidden");
        featureAttribute.Should().Contain("IsEnabledAsync");
    }

    [Fact]
    public void TenantAccessGuard_Deve_Bloquear_Tenant_Suspenso_Cancelado_E_Module_Feature_Desabilitados()
    {
        var guard = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Application", "Saas", "TenantAccessGuard.cs"));

        guard.Should().Contain("ATIVO");
        guard.Should().NotContain("SUSPENSO");
        guard.Should().NotContain("CANCELADO");
        guard.Should().Contain("EnsureModuleAsync");
        guard.Should().Contain("EnsureFeatureAsync");
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
