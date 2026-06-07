namespace Sigov.Application.Health;

public sealed record HealthSummaryResponse(
    string Status,
    string Service,
    string Version,
    IReadOnlyCollection<HealthCheckResult> Checks);
