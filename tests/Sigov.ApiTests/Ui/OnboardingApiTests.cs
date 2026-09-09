using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class OnboardingApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public OnboardingApiTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Onboarding_Anonimo_Deve_Ser_Negado()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/onboarding/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Implantação assistida sigov");
        body.Should().NotContain("Configurar tenant");
    }
}
