using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Health;
using Sigov.Application.Release;

namespace Sigov.Infrastructure.Health;

public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IVersionInfoProvider _versionInfoProvider;
    private readonly IReleaseInfoProvider _releaseInfoProvider;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(IServiceProvider serviceProvider, IVersionInfoProvider versionInfoProvider, IReleaseInfoProvider releaseInfoProvider, ILogger<HealthCheckService> logger)
    {
        _serviceProvider = serviceProvider;
        _versionInfoProvider = versionInfoProvider;
        _releaseInfoProvider = releaseInfoProvider;
        _logger = logger;
    }

    public HealthSummaryResponse GetLive() => new("Healthy", _versionInfoProvider.Service, _versionInfoProvider.Version, Array.Empty<HealthCheckResult>());

    public async Task<HealthSummaryResponse> GetReadyAsync(CancellationToken cancellationToken)
    {
        var results = new List<HealthCheckResult>();
        IReadOnlyCollection<IHealthCheck> checks;
        try
        {
            checks = _serviceProvider.GetServices<IHealthCheck>().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao resolver dependências dos health checks de readiness.");
            results.Add(HealthCheckResult.Unhealthy("dependencies", "Dependências de readiness indisponíveis."));
            return new HealthSummaryResponse("Unhealthy", _versionInfoProvider.Service, _versionInfoProvider.Version, results);
        }

        foreach (var check in checks.Where(check => check.IncludeInReady))
        {
            results.Add(await RunSafelyAsync(check, cancellationToken).ConfigureAwait(false));
        }

        var status = results.Any(result => result.Status == HealthCheckStatus.Unhealthy) ? "Unhealthy" : "Ready";
        return new HealthSummaryResponse(status, _versionInfoProvider.Service, _versionInfoProvider.Version, results);
    }

    public async Task<HealthCheckResult> GetDatabaseAsync(CancellationToken cancellationToken) => await RunByNameAsync("db", cancellationToken).ConfigureAwait(false);

    public async Task<HealthCheckResult> GetOutboxAsync(CancellationToken cancellationToken) => await RunByNameAsync("outbox", cancellationToken).ConfigureAwait(false);

    public Task<HealthCheckResult> GetStorageAsync(CancellationToken cancellationToken) =>
        RunByNameAsync("storage", cancellationToken);

    public ReleaseInfoResponse GetVersion() => _releaseInfoProvider.GetReleaseInfo();

    private async Task<HealthCheckResult> RunByNameAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            var check = _serviceProvider.GetServices<IHealthCheck>().Single(healthCheck => healthCheck.Name == name);
            return await RunSafelyAsync(check, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao resolver health check {HealthCheckName}.", name);
            return HealthCheckResult.Unhealthy(name, "Dependência indisponível.");
        }
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
