using Microsoft.Extensions.Logging;
using Sigov.Application.Health;

namespace Sigov.Infrastructure.Health;

public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IReadOnlyCollection<IHealthCheck> _checks;
    private readonly IVersionInfoProvider _versionInfoProvider;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(IEnumerable<IHealthCheck> checks, IVersionInfoProvider versionInfoProvider, ILogger<HealthCheckService> logger)
    {
        _checks = checks.ToArray();
        _versionInfoProvider = versionInfoProvider;
        _logger = logger;
    }

    public HealthSummaryResponse GetLive() => new("Healthy", _versionInfoProvider.Service, _versionInfoProvider.Version, Array.Empty<HealthCheckResult>());

    public async Task<HealthSummaryResponse> GetReadyAsync(CancellationToken cancellationToken)
    {
        var results = new List<HealthCheckResult>();
        foreach (var check in _checks.Where(check => check.IncludeInReady))
        {
            results.Add(await RunSafelyAsync(check, cancellationToken).ConfigureAwait(false));
        }

        var status = results.Any(result => result.Status == HealthCheckStatus.Unhealthy) ? "Unhealthy" : "Ready";
        return new HealthSummaryResponse(status, _versionInfoProvider.Service, _versionInfoProvider.Version, results);
    }

    public async Task<HealthCheckResult> GetDatabaseAsync(CancellationToken cancellationToken) => await RunByNameAsync("db", cancellationToken).ConfigureAwait(false);

    public async Task<HealthCheckResult> GetOutboxAsync(CancellationToken cancellationToken) => await RunByNameAsync("outbox", cancellationToken).ConfigureAwait(false);

    public HealthCheckResult GetStorage()
    {
        var storage = _checks.Single(check => check.Name == "storage");
        return storage.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public object GetVersion() => new { application = _versionInfoProvider.Application, version = _versionInfoProvider.Version };

    private async Task<HealthCheckResult> RunByNameAsync(string name, CancellationToken cancellationToken)
    {
        var check = _checks.Single(healthCheck => healthCheck.Name == name);
        return await RunSafelyAsync(check, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult> RunSafelyAsync(IHealthCheck check, CancellationToken cancellationToken)
    {
        try
        {
            return await check.CheckAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha em health check {HealthCheckName}.", check.Name);
            return HealthCheckResult.Unhealthy(check.Name, "Dependência indisponível.", new Dictionary<string, object?> { ["error"] = "unavailable" });
        }
    }
}
