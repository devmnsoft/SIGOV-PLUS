using System.Reflection;
using Sigov.Application.Health;

namespace Sigov.Infrastructure.Health;

public sealed class VersionInfoProvider : IVersionInfoProvider
{
    private readonly Assembly _assembly;

    public VersionInfoProvider() => _assembly = Assembly.GetEntryAssembly() ?? typeof(VersionInfoProvider).Assembly;

    public string Application => "sigov";
    public string Service => "sigov API";
    public string Version => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_VERSION")) ?? _assembly.GetName().Version?.ToString() ?? "dev";
    public string? Commit => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_COMMIT_SHA"));
    public string EnvironmentName => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    public string? BuildDate => EmptyAsNull(Environment.GetEnvironmentVariable("SIGOV_BUILD_DATE"));

    private static string? EmptyAsNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
