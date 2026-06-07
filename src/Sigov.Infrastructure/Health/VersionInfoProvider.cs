using System.Reflection;
using Sigov.Application.Health;

namespace Sigov.Infrastructure.Health;

public sealed class VersionInfoProvider : IVersionInfoProvider
{
    private readonly Assembly _assembly;

    public VersionInfoProvider() => _assembly = Assembly.GetEntryAssembly() ?? typeof(VersionInfoProvider).Assembly;

    public string Application => "sigov";
    public string Service => "sigov API";
    public string Version => _assembly.GetName().Version?.ToString() ?? "dev";
}
