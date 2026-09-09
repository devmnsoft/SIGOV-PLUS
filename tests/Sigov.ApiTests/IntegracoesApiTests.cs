using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sigov.Application.Integracoes;
using Xunit;

namespace Sigov.ApiTests;

public sealed class IntegracoesApiTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;
    public IntegracoesApiTests(SigovApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DashboardAnonimo_RetornaUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/integracoes/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceberWebhookSemTenant_NaoVazaStackTrace()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/integracoes/webhooks/receber/dev", new WebhookReceberRequest("Ping", new { ok = true }, "idem-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(body.Contains("StackTrace", StringComparison.OrdinalIgnoreCase));
    }
}
