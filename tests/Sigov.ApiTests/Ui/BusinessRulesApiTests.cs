using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class BusinessRulesApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public BusinessRulesApiTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Regras_De_Negocio_Anonimo_Deve_Ser_Negado()
    {
        using var client = _factory.CreateClient();
        using var listResponse = await client.GetAsync("/api/regras-negocio");
        using var moduleResponse = await client.GetAsync("/api/regras-negocio/Core");

        listResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        moduleResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await moduleResponse.Content.ReadAsStringAsync();
        body.Should().NotContain("Documento CPF/CNPJ");
    }
}
