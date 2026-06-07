using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class ModuleCatalogApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModuleCatalogApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Catalogo_De_Modulos_Deve_Responder_Rotas_Comerciais()
    {
        using var client = _factory.CreateClient();
        using var listResponse = await client.GetAsync("/api/ui/modulos").ConfigureAwait(false);
        using var detailResponse = await client.GetAsync("/api/ui/modulos/core").ConfigureAwait(false);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        body.Should().Contain("Core e Cadastros");
        body.Should().Contain("Financeiro/SIAFIC");
    }
}
