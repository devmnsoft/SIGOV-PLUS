using Microsoft.Extensions.Logging;
using Sigov.Application.Lgpd;

namespace Sigov.Infrastructure.Lgpd;

public sealed class LgpdAccessLogger : ILgpdAccessLogger
{
    private readonly ILogger<LgpdAccessLogger> _logger;

    public LgpdAccessLogger(ILogger<LgpdAccessLogger> logger) => _logger = logger;

    public void LogPersonalDataAccess(string operation, string fieldName, long? tenantId, Guid? correlationId) =>
        _logger.LogInformation("Acesso LGPD registrado. Operation={Operation} Field={FieldName} TenantId={TenantId} CorrelationId={CorrelationId}", operation, fieldName, tenantId, correlationId);
}
