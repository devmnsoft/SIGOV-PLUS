namespace Sigov.Application.Release;

public sealed class SmokeTestPlanService : ISmokeTestPlanService
{
    private static readonly string[] CommonEndpoints =
    {
        "/api/health",
        "/api/health/live",
        "/api/health/ready",
        "/api/health/db",
        "/api/health/outbox",
        "/api/health/version"
    };

    public IReadOnlyCollection<string> GetRequiredEndpoints(string environment) =>
        string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            ? CommonEndpoints.Concat(new[] { "/swagger" }).ToArray()
            : CommonEndpoints;
}
