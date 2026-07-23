using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Infrastructure.Data;

namespace Sigov.IntegrationTests;

public sealed class WebRuntimeSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebRuntimeSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5432;Database=sigov_test;Username=sigov;Password=sigov"));
    }

    [Fact]
    public async Task LoginPage_ShouldReturnValidHtmlWithoutStackTrace()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Auth/Login");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("<html", Exactly.Once());
        html.Should().Contain("login", StringComparison.OrdinalIgnoreCase);
        html.Should().NotContain("System.");
        html.Should().NotContain("StackTrace", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedRoute_ShouldRedirectAnonymousUserToLogin()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/MinhaCentral");

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.Found, System.Net.HttpStatusCode.Unauthorized);
        if (response.Headers.Location is not null)
        {
            response.Headers.Location.ToString().Should().Contain("/Auth/Login");
        }
    }

    [Fact]
    public void WebApplication_ShouldBuildDependencyInjectionContainerAndExposeCssAssets()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>().Should().NotBeNull();

        File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "css", "site.css")).Should().BeTrue();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "css", "sigov-base.css")).Should().BeTrue();
    }
}
