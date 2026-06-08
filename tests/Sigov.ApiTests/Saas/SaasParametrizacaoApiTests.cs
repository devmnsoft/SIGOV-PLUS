using FluentAssertions;
using Sigov.Api.Controllers;
using Xunit;

namespace Sigov.ApiTests.Saas;

public sealed class SaasParametrizacaoApiTests
{
    [Fact]
    public void Controllers_saas_foram_criados()
    {
        typeof(SaasModulesController).Name.Should().Be("SaasModulesController");
        typeof(SaasProfilesController).Name.Should().Be("SaasProfilesController");
        typeof(TenantParametersController).Name.Should().Be("TenantParametersController");
        typeof(TenantContextController).Name.Should().Be("TenantContextController");
    }
}
