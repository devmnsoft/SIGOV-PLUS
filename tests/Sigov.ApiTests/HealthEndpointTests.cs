using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class HealthEndpointTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public HealthEndpointTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Deve_Retornar_Sucesso()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health");

        response.IsSuccessStatusCode.Should().BeTrue();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("sigov API");
    }
}
