using Microsoft.Extensions.Hosting;

namespace Sigov.Application.Release;

public sealed class ReleaseInfoProvider : IReleaseInfoProvider
{
    private const string DefaultVersion = "v1.0.0";
    private readonly IHostEnvironment _environment;

    public ReleaseInfoProvider(IHostEnvironment environment) => _environment = environment;

    public ReleaseInfoResponse GetReleaseInfo() => new(
        Application: "sigov",
        Version: GetValue("SIGOV_VERSION") ?? ReadVersionFile() ?? DefaultVersion,
        ReleaseChannel: GetValue("SIGOV_RELEASE_CHANNEL") ?? "stable",
        CommitSha: GetValue("SIGOV_COMMIT_SHA"),
        BuildDate: GetValue("SIGOV_BUILD_DATE"),
        Environment: _environment.EnvironmentName,
        Database: "sigov",
        Schema: "sigov");

    private string? ReadVersionFile()
    {
        var candidates = new[]
        {
            Path.Combine(_environment.ContentRootPath, "VERSION"),
            Path.Combine(Directory.GetCurrentDirectory(), "VERSION")
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                var value = File.ReadAllText(candidate).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? GetValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
