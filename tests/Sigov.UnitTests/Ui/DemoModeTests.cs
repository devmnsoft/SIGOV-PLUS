using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sigov.Application.Demo;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class DemoModeTests
{
    [Fact]
    public void DemoMode_Nao_Deve_Habilitar_Em_Production_Por_Padrao()
    {
        var service = new DemoModeService(Options.Create(new DemoModeOptions { Enabled = true }), new FakeHostEnvironment("Production"));

        service.GetState().IsEnabled.Should().BeFalse();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "sigov-tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
