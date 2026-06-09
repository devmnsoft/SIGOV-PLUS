using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests.Ui;

public sealed class UserPreferencesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UserPreferencesApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Preferencias_Deve_Exigir_Usuario()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/ui/preferencias/tema");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Usuário obrigatório");
    }
}
