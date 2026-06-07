using Sigov.Application.Release;

namespace Sigov.Application.Health;

public interface IHealthCheckService
{
    HealthSummaryResponse GetLive();
    Task<HealthSummaryResponse> GetReadyAsync(CancellationToken cancellationToken);
    Task<HealthCheckResult> GetDatabaseAsync(CancellationToken cancellationToken);
    Task<HealthCheckResult> GetOutboxAsync(CancellationToken cancellationToken);
    HealthCheckResult GetStorage();
    ReleaseInfoResponse GetVersion();
}
