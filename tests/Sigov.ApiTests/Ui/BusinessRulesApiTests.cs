using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class BusinessRulesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BusinessRulesApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Regras_De_Negocio_Deve_Responder_Lista_E_Modulo()
    {
        using var client = _factory.CreateClient();
        using var listResponse = await client.GetAsync("/api/regras-negocio");
        using var moduleResponse = await client.GetAsync("/api/regras-negocio/Core");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        moduleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await moduleResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Documento CPF/CNPJ");
    }
}
