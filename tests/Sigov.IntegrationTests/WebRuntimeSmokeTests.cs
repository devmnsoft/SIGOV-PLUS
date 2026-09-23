using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.IntegrationTests;

public sealed class WebRuntimeSmokeTests : IClassFixture<SigovWebFactory>
{
    private readonly SigovWebFactory _factory;

    public WebRuntimeSmokeTests(SigovWebFactory factory)
    {
        _factory = factory;
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

    [Theory]
    [InlineData("/OrdemServico/Ordens/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/ordens-servico/00000000-0000-0000-0000-000000000001/historico")]
    public async Task OrdemServicoExecutionRoutes_ShouldDenyAnonymousAccess(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.Found, System.Net.HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/ComprasEmpresariais/Recebimentos")]
    [InlineData("/ComprasEmpresariais/Pedidos/00000000-0000-0000-0000-000000000001/Receber")]
    public async Task PurchaseReceiptRoutes_ShouldDenyAnonymousAccess(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.Found, System.Net.HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/Governanca/QualidadeDados")]
    [InlineData("/Governanca/IntegracoesInternas")]
    [InlineData("/QualidadeDados")]
    [InlineData("/IntegracoesInternas")]
    [InlineData("/Governanca/Ocorrencias/qualidade/1")]
    [InlineData("/Governanca/Ocorrencias/pendencia/1")]
    public async Task GovernanceNavigationRoutes_ShouldBeProtectedWithoutAmbiguousMatch(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);

        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.Redirect, System.Net.HttpStatusCode.Found, System.Net.HttpStatusCode.Unauthorized);
        response.Headers.Location?.ToString().Should().Contain("/Auth/Login");
    }

    [Fact]
    public void GovernanceRoutes_ShouldHaveOneGetEndpointAndCanonicalController()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>().ToArray();

        foreach (var route in new[] { "/Governanca/QualidadeDados", "/Governanca/IntegracoesInternas", "/QualidadeDados", "/IntegracoesInternas" })
        {
            var matches = endpoints.Where(endpoint =>
                string.Equals('/' + endpoint.RoutePattern.RawText?.TrimStart('/'), route, StringComparison.OrdinalIgnoreCase) &&
                (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase) ?? true)).ToArray();
            matches.Should().ContainSingle($"GET {route} must resolve deterministically");
            var action = matches[0].Metadata.GetMetadata<ControllerActionDescriptor>();
            action?.ControllerName.Should().Be("GovernancaTransversal");
            action?.ActionName.Should().Be(route.StartsWith("/Governanca/", StringComparison.OrdinalIgnoreCase)
                ? route.EndsWith("QualidadeDados", StringComparison.OrdinalIgnoreCase) ? "QualidadeDados" : "IntegracoesInternas"
                : route.EndsWith("QualidadeDados", StringComparison.OrdinalIgnoreCase) ? "QualidadeDadosAlias" : "IntegracoesInternasAlias");
            matches[0].Metadata.GetMetadata<IAuthorizeData>().Should().NotBeNull();
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
