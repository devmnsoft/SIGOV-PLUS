using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
