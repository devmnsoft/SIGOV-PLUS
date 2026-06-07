using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sigov.Application.Configuration;
using Xunit;

namespace Sigov.UnitTests.Hardening;

public sealed class OptionsProductionValidationTests
{
    [Fact]
    public void Production_Deve_Falhar_Sem_Jwt_Secret()
    {
        var result = CreateValidator("Production").Validate(null, CreateProductionOptions(jwtSecret: null));
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Production_Deve_Falhar_Com_Cors_Wildcard()
    {
        var options = CreateProductionOptions(jwtSecret: "12345678901234567890123456789012");
        options.Security.CorsAllowedOrigins = new[] { "*" };

        CreateValidator("Production").Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Production_Deve_Falhar_Com_Seed_Demo()
    {
        var options = CreateProductionOptions(jwtSecret: "12345678901234567890123456789012");
        options.Seed.Demo = true;

        CreateValidator("Production").Validate(null, options).Failed.Should().BeTrue();
    }


    [Fact]
    public void Production_Deve_Falhar_Com_Jwt_Placeholder()
    {
        var result = CreateValidator("Production").Validate(null, CreateProductionOptions(jwtSecret: "REPLACE_WITH_32_PLUS_CHARACTER_SECRET"));

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Development_Deve_Permitir_Seed_Demo()
    {
        var options = new SigovOptions { Seed = new SeedOptions { Demo = true } };
        CreateValidator("Development").Validate(null, options).Succeeded.Should().BeTrue();
    }

    private static SigovOptionsValidator CreateValidator(string environment) => new(new TestEnvironment(environment));

    private static SigovOptions CreateProductionOptions(string? jwtSecret) => new()
    {
        Jwt = new JwtOptions { Secret = jwtSecret },
        Security = new SecurityOptions { CorsAllowedOrigins = new[] { "https://app.example.gov.br" } },
        Seed = new SeedOptions { Demo = false }
    };

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "sigov-tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
