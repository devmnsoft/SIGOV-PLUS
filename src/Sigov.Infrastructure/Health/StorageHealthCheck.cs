using Microsoft.Extensions.Options;
using Sigov.Application.Configuration;
using Sigov.Application.Health;

namespace Sigov.Infrastructure.Health;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly SigovOptions _options;

    public StorageHealthCheck(IOptions<SigovOptions> options) => _options = options.Value;

    public string Name => "storage";
    public bool IncludeInReady => false;

    public Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var details = new Dictionary<string, object?> { ["provider"] = _options.Storage.Provider, ["maxUploadBytes"] = _options.Storage.MaxUploadBytes };
        return Task.FromResult(HealthCheckResult.Healthy(Name, "Storage configurado.", details));
    }
}
