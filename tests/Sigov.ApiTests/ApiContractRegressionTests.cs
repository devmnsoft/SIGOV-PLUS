using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Sigov.ApiTests;

public sealed class ApiContractRegressionTests : IClassFixture<SigovApiFactory>
{
    private readonly SigovApiFactory _factory;

    public ApiContractRegressionTests(SigovApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/health", HttpStatusCode.OK)]
    [InlineData("/api/health/live", HttpStatusCode.OK)]
    [InlineData("/api/health/version", HttpStatusCode.OK)]
    public async Task PublicHealthEndpoints_Deve_Retornar_ApiResponse_Sem_Autenticacao(string path, HttpStatusCode expectedStatus)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(expectedStatus);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
        content.Should().NotContain("StackTrace");
        content.Should().NotContain("Exception");
    }

    [Fact]
    public async Task VersionEndpoint_Deve_Expor_Metadados_De_Release_Final()
    {
        var previousVersion = Environment.GetEnvironmentVariable("SIGOV_VERSION");
        var previousCommit = Environment.GetEnvironmentVariable("SIGOV_COMMIT_SHA");
        var previousBuildDate = Environment.GetEnvironmentVariable("SIGOV_BUILD_DATE");
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("SIGOV_VERSION", "v1.0.0");
            Environment.SetEnvironmentVariable("SIGOV_COMMIT_SHA", "test-sha");
            Environment.SetEnvironmentVariable("SIGOV_BUILD_DATE", "2026-06-07T00:00:00Z");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            using var client = _factory.CreateClient();
            using var response = await client.GetAsync("/api/health/version");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("\"application\":\"sigov\"");
            content.Should().Contain("\"version\":\"v1.0.0\"");
            content.Should().Contain("\"commitSha\":\"test-sha\"");
            content.Should().Contain("\"environment\":");
            content.Should().Contain("\"buildDate\":\"2026-06-07T00:00:00Z\"");
            content.Should().Contain("\"releaseChannel\":");
            content.Should().Contain("\"database\":\"sigov\"");
            content.Should().Contain("\"schema\":\"sigov\"");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGOV_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("SIGOV_COMMIT_SHA", previousCommit);
            Environment.SetEnvironmentVariable("SIGOV_BUILD_DATE", previousBuildDate);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        }
    }

    [Fact]
    public void ApiDescriptions_Nao_Devem_Conter_Metodo_E_Caminho_Duplicados()
    {
        var descriptions = _factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items);

        var duplicates = descriptions
            .GroupBy(description => $"{description.HttpMethod?.ToUpperInvariant()} {NormalizePath(description.RelativePath)}")
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(ActionName))}")
            .ToArray();

        duplicates.Should().BeEmpty("cada combinação método/caminho OpenAPI deve possuir uma única action");
    }

    [Fact]
    public async Task SwaggerJson_Deve_Ser_Valido_Sem_Rotas_Ou_OperationIds_Duplicados()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        document.RootElement.GetProperty("openapi").GetString().Should().NotBeNullOrWhiteSpace();

        var operations = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => HttpMethods.Contains(operation.Name))
                .Select(operation => new
                {
                    Key = $"{operation.Name.ToUpperInvariant()} {NormalizePath(path.Name)}",
                    OperationId = operation.Value.GetProperty("operationId").GetString()
                }))
            .ToArray();

        operations.Select(operation => operation.Key).Should().OnlyHaveUniqueItems();
        operations.Select(operation => operation.OperationId).Should().NotContainNulls().And.OnlyHaveUniqueItems();
        operations.Count(operation => operation.Key == "GET api/almoxarifado/dashboard").Should().Be(1);
        operations.Count(operation => operation.Key == "GET api/bloco6/almoxarifado/dashboard").Should().Be(1);
    }

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace"
    };

    private static string NormalizePath(string? path)
    {
        var withoutQuery = (path ?? string.Empty).Split('?', 2)[0].Trim('/').ToLowerInvariant();
        return Regex.Replace(withoutQuery, @"\{([^}:]+)(?::[^}]+)?\}", "{$1}");
    }

    private static string ActionName(ApiDescription description) =>
        description.ActionDescriptor is ControllerActionDescriptor action
            ? $"{action.ControllerTypeInfo.FullName}.{action.ActionName}"
            : description.ActionDescriptor.DisplayName ?? "action desconhecida";
}
