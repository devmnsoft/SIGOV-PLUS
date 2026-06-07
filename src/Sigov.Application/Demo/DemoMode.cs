using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Sigov.Application.Demo;

public sealed class DemoModeOptions
{
    public bool Enabled { get; set; }

    public bool ShowBanner { get; set; } = true;

    public bool AllowSampleData { get; set; }

    public bool AllowProduction { get; set; }
}

public sealed record DemoModeState(bool IsEnabled, bool ShowBanner, bool AllowSampleData, string Label);

public interface IDemoModeService
{
    DemoModeState GetState();
}

public sealed class DemoModeService : IDemoModeService
{
    private readonly IHostEnvironment _environment;
    private readonly DemoModeOptions _options;

    public DemoModeService(IOptions<DemoModeOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public DemoModeState GetState()
    {
        var enabled = _options.Enabled && (!_environment.IsProduction() || _options.AllowProduction);
        return new DemoModeState(enabled, enabled && _options.ShowBanner, enabled && _options.AllowSampleData, enabled ? "Ambiente de demonstração/homologação" : "Operação real");
    }
}
