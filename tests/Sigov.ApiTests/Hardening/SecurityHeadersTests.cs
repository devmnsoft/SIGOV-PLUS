using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Hardening;

public sealed class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Deve_Retornar_Headers_De_Seguranca()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/health/live");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        response.Headers.GetValues("Permissions-Policy").Should().Contain("camera=(), microphone=(), geolocation=()");
    }
}
