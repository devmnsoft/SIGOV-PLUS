using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class SaneamentoApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;
    public SaneamentoApiTests(SigovApiFactory factory) => _factory = factory;
    [Fact]
    public async Task Dashboard_Anonimo_Deve_Retornar_Unauthorized()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/saneamento/dashboard");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
