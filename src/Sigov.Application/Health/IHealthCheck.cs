namespace Sigov.Application.Health;

public interface IHealthCheck
{
    string Name { get; }
    bool IncludeInReady { get; }
    Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken);
}
