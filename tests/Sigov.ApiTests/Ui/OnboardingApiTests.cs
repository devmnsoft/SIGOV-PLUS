using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class OnboardingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OnboardingApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Onboarding_Deve_Retornar_Jornada_Padrao()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/onboarding/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Implantação assistida sigov");
        body.Should().Contain("Configurar tenant");
    }
}
