using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sigov.Testing;

namespace Sigov.UnitTests;

public sealed class TestHostEnvironmentTests
{
    [Fact]
    public void Environment_implements_both_host_contracts_with_existing_roots()
    {
        var environment = new TestHostEnvironment();

        environment.Should().BeAssignableTo<IHostEnvironment>();
        environment.Should().BeAssignableTo<IWebHostEnvironment>();
        Directory.Exists(environment.ContentRootPath).Should().BeTrue();
        Directory.Exists(environment.WebRootPath).Should().BeTrue();
        environment.ContentRootFileProvider.Should().NotBeNull();
        environment.WebRootFileProvider.Should().NotBeNull();
    }

    [Fact]
    public void Host_contracts_resolve_the_same_environment_instance()
    {
        var services = new ServiceCollection().AddSigovTestHostEnvironment();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHostEnvironment>()
            .Should().BeSameAs(provider.GetRequiredService<IWebHostEnvironment>());
    }
}
