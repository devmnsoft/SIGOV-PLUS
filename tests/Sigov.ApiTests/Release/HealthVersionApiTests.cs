using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Release;

public sealed class HealthVersionApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public HealthVersionApiTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HealthVersion_Deve_Retornar_Versao_E_Schema_Sigov()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health/version");

        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("sigov");
        content.Should().Contain("version");
        content.Should().Contain("releaseChannel");
    }
}
