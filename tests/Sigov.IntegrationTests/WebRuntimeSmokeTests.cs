using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.IntegrationTests;

public sealed class WebRuntimeSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebRuntimeSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5432;Database=sigov_test;Username=sigov;Password=sigov");
            builder.UseSetting("Sigov:Database:MigrationMode", "Disabled");
            builder.UseSetting("Sigov:Storage:LocalPath", Path.Combine(Path.GetTempPath(), "sigov-web-smoke-storage"));
        });
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
        html.Contains("login", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        html.Should().NotContain("System.");
        html.Contains("StackTrace", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticatedRoute_ShouldRedirectAnonymousUserToLoginOrReturnUnauthorized()
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
    public void WebApplication_ShouldBuildDependencyInjectionContainerWithRealDapperService()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<DapperContext>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<NpgsqlConnectionFactory>().Should().NotBeNull();
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/css/sigov-base.css")]
    [InlineData("/css/sigov-tokens.css")]
    public async Task CssAssets_ShouldBeServedByHttpWithoutHtmlError(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);
        var css = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/css");
        css.Should().NotBeNullOrWhiteSpace();
        css.Contains("<html", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        css.Contains("StackTrace", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }
}
