using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sigov.Application.Release;
using Xunit;

namespace Sigov.UnitTests.Release;

public sealed class ReleaseInfoProviderTests : IDisposable
{
    [Fact]
    public void GetReleaseInfo_Deve_Retornar_Sigov_E_Versao()
    {
        var provider = new ReleaseInfoProvider(new TestEnvironment("Production"));

        var result = provider.GetReleaseInfo();

        result.Application.Should().Be("sigov");
        result.Version.Should().NotBeNullOrWhiteSpace();
        result.Database.Should().Be("sigov");
        result.Schema.Should().Be("sigov");
    }

    [Fact]
    public void GetReleaseInfo_Deve_Respeitar_Variaveis_De_Build()
    {
        Environment.SetEnvironmentVariable("SIGOV_VERSION", "v9.9.9-test");
        Environment.SetEnvironmentVariable("SIGOV_COMMIT_SHA", "abc123");
        Environment.SetEnvironmentVariable("SIGOV_BUILD_DATE", "2026-06-07T00:00:00Z");
        Environment.SetEnvironmentVariable("SIGOV_RELEASE_CHANNEL", "homologation");
        var provider = new ReleaseInfoProvider(new TestEnvironment("Homologation"));

        var result = provider.GetReleaseInfo();

        result.Version.Should().Be("v9.9.9-test");
        result.CommitSha.Should().Be("abc123");
        result.BuildDate.Should().Be("2026-06-07T00:00:00Z");
        result.ReleaseChannel.Should().Be("homologation");
        result.Environment.Should().Be("Homologation");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SIGOV_VERSION", null);
        Environment.SetEnvironmentVariable("SIGOV_COMMIT_SHA", null);
        Environment.SetEnvironmentVariable("SIGOV_BUILD_DATE", null);
        Environment.SetEnvironmentVariable("SIGOV_RELEASE_CHANNEL", null);
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
