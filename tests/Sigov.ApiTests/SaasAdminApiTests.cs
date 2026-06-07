using FluentAssertions;
using Xunit;
using Sigov.Testing;

namespace Sigov.ApiTests;

public sealed class SaasAdminApiTests
{
    [Fact]
    public void SaasAdmin_Deve_Ter_Controller_E_Api()
    {
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("src/Sigov.Api/Controllers/Saas/SaasAdminController.cs")).Should().BeTrue();
    }
}
