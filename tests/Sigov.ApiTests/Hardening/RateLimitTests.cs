using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Sigov.ApiTests.Hardening;

public sealed class RateLimitTests
{
    [Fact]
    public async Task Health_Live_Nao_Deve_Ser_Bloqueado_Pelo_RateLimit()
    {
        await using var factory = new SigovApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Sigov:RateLimit:RequestsPerMinutePerTenant"] = "1" });
            });
        });
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/api/health/live");
        using var second = await client.GetAsync("/api/health/live");
        using var third = await client.GetAsync("/api/health/live");

        first.IsSuccessStatusCode.Should().BeTrue();
        second.IsSuccessStatusCode.Should().BeTrue();
        third.IsSuccessStatusCode.Should().BeTrue();
    }
}
