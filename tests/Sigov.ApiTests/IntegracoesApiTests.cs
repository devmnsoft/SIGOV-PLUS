using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Sigov.Application.Integracoes;
using Xunit;

namespace Sigov.ApiTests;

public sealed class IntegracoesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public IntegracoesApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task DashboardSemTenant_RetornaBadRequestOuForbidden()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/integracoes/dashboard");
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReceberWebhookSemTenant_NaoVazaStackTrace()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/integracoes/webhooks/receber/dev", new WebhookReceberRequest("Ping", new { ok = true }, "idem-1"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(body.Contains("StackTrace", StringComparison.OrdinalIgnoreCase));
    }
}
