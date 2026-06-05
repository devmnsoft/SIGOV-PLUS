using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class SaneamentoApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public SaneamentoApiTests(WebApplicationFactory<Program> factory) => _factory = factory;
    [Fact]
    public async Task Dashboard_Sem_Tenant_Deve_Bloquear_Acesso_Ao_Modulo()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/saneamento/dashboard").ConfigureAwait(false);
        ((int)response.StatusCode).Should().BeOneOf(400, 403);
    }
}
