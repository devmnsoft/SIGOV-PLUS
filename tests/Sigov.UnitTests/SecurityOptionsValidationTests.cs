using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sigov.Application.Configuration;
using Xunit;

namespace Sigov.UnitTests;

public sealed class SecurityOptionsValidationTests
{
    [Fact]
    public void Validate_DeveFalharProductionSemJwtSecret()
    {
        var validator = new SigovOptionsValidator(new TestEnvironment("Production"));
        var options = new SigovOptions
        {
            Security = new SecurityOptions { CorsAllowedOrigins = new[] { "https://app.example.gov.br" } },
            Jwt = new JwtOptions { Secret = null },
            Seed = new SeedOptions { Demo = false }
        };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_DeveFalharProductionComCorsWildcard()
    {
        var validator = new SigovOptionsValidator(new TestEnvironment("Production"));
        var options = new SigovOptions
        {
            Security = new SecurityOptions { CorsAllowedOrigins = new[] { "*" } },
            Jwt = new JwtOptions { Secret = "12345678901234567890123456789012" },
            Seed = new SeedOptions { Demo = false }
        };

        validator.Validate(null, options).Failed.Should().BeTrue();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public TestEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "sigov-tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
