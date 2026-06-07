namespace Sigov.Application.Health;

public sealed record HealthCheckResult(
    string Name,
    HealthCheckStatus Status,
    string Description,
    IReadOnlyDictionary<string, object?> Details)
{
    public static HealthCheckResult Healthy(string name, string description, IReadOnlyDictionary<string, object?>? details = null) =>
        new(name, HealthCheckStatus.Healthy, description, details ?? new Dictionary<string, object?>());

    public static HealthCheckResult Degraded(string name, string description, IReadOnlyDictionary<string, object?>? details = null) =>
        new(name, HealthCheckStatus.Degraded, description, details ?? new Dictionary<string, object?>());

    public static HealthCheckResult Unhealthy(string name, string description, IReadOnlyDictionary<string, object?>? details = null) =>
        new(name, HealthCheckStatus.Unhealthy, description, details ?? new Dictionary<string, object?>());
}
