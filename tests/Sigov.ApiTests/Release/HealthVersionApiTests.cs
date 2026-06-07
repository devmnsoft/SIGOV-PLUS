using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Release;

public sealed class HealthVersionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthVersionApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HealthVersion_Deve_Retornar_Versao_E_Schema_Sigov()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health/version").ConfigureAwait(false);

        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        content.Should().Contain("sigov");
        content.Should().Contain("version");
        content.Should().Contain("releaseChannel");
    }
}
