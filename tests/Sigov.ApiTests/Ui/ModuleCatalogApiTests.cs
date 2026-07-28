using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class ModuleCatalogApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public ModuleCatalogApiTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Catalogo_De_Modulos_Deve_Responder_Rotas_Comerciais()
    {
        using var client = _factory.CreateClient();
        using var listResponse = await client.GetAsync("/api/ui/modulos");
        using var detailResponse = await client.GetAsync("/api/ui/modulos/core");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Core e Cadastros");
        body.Should().Contain("Financeiro/SIAFIC");
    }
}
