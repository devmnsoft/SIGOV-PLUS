namespace Sigov.Application.Parameters;

public sealed record ModuleParameterValue(string Code, string Name, string Type, string ValueJson, bool Sensitive, DateTimeOffset UpdatedAt);
public sealed record ModuleParameterHistory(long Id, string Code, string? PreviousValueJson, string ValueJson, long? ChangedBy, string CorrelationId, DateTimeOffset ChangedAt);
public sealed record SaveModuleParametersRequest(IReadOnlyDictionary<string, string> Values);

public interface IModuleParameterRepository
{
    Task<IReadOnlyCollection<ModuleParameterValue>> ListAsync(long tenantId, string module, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleParameterHistory>> HistoryAsync(long tenantId, string module, int page, int pageSize, CancellationToken cancellationToken);
    Task SaveAsync(long tenantId, string module, IReadOnlyDictionary<string, string> values, long? userId, string correlationId, CancellationToken cancellationToken);
}

public interface IModuleParameterService
{
    Task<IReadOnlyCollection<ModuleParameterValue>> ListAsync(long tenantId, string module, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ModuleParameterHistory>> HistoryAsync(long tenantId, string module, int page, int pageSize, CancellationToken cancellationToken);
    Task SaveAsync(long tenantId, string module, SaveModuleParametersRequest request, long? userId, string correlationId, CancellationToken cancellationToken);
}
