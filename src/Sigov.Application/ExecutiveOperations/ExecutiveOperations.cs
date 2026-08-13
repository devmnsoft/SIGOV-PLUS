namespace Sigov.Application.ExecutiveOperations;

public sealed record ExecutiveMetric(string Module, string Key, string Label, decimal Value, string Severity, string? Url);
public sealed record OperationalItem(long Id, string Module, string Type, string Title, string Severity, string Status, string? ReferenceType, long? ReferenceId, string? Url, DateTimeOffset CreatedAt);
public sealed record GovernanceSummary(IReadOnlyList<ExecutiveMetric> Indicators, IReadOnlyList<OperationalItem> Pendencies, int UnreadNotifications, int CriticalRisks);
public sealed record NotificationItem(long Id, string Module, string Type, string Title, string Message, string Severity, string? Url, bool Read, bool Archived, DateTimeOffset CreatedAt);
public sealed record IntegrationEventItem(long Id, string SourceModule, string TargetModule, string EventType, string Status, string? ReferenceType, long? ReferenceId, string CorrelationId, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? ProcessedAt);
public sealed record DataQualitySummary(string Module, int Open, int Critical, decimal Score);
public sealed record DataQualityItem(long Id, string Module, string Type, string Severity, string Description, string Status, string? ReferenceType, long? ReferenceId, DateTimeOffset CreatedAt);
public sealed record AssistantCommand(string Assistant, string Step, string Payload, bool Complete);
public sealed record AssistantExecution(long Id, string Assistant, string Step, string Status, DateTimeOffset UpdatedAt);
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long Total);
public sealed record OperationFilter(string? Module = null, string? Severity = null, string? Status = null, int Page = 1, int PageSize = 25);

public interface IExecutiveOperationsRepository
{
    Task<GovernanceSummary> GovernanceAsync(long tenantId, long? userId, CancellationToken ct);
    Task<Page<OperationalItem>> PendenciesAsync(long tenantId, OperationFilter filter, CancellationToken ct);
    Task<IReadOnlyList<ExecutiveMetric>> IndicatorsAsync(long tenantId, string? module, CancellationToken ct);
    Task<Page<NotificationItem>> NotificationsAsync(long tenantId, long userId, OperationFilter filter, bool unreadOnly, CancellationToken ct);
    Task MarkNotificationAsync(long tenantId, long userId, long? id, bool archive, CancellationToken ct);
    Task<string> GetPreferencesAsync(long tenantId, long userId, CancellationToken ct);
    Task SetPreferencesAsync(long tenantId, long userId, string preferences, CancellationToken ct);
    Task<Page<IntegrationEventItem>> IntegrationsAsync(long tenantId, OperationFilter filter, CancellationToken ct);
    Task ChangeIntegrationAsync(long tenantId, long id, string status, long? userId, string correlationId, CancellationToken ct);
    Task<IReadOnlyList<DataQualitySummary>> QualitySummaryAsync(long tenantId, CancellationToken ct);
    Task<Page<DataQualityItem>> QualityAsync(long tenantId, OperationFilter filter, CancellationToken ct);
    Task ReprocessQualityAsync(long tenantId, long? userId, string correlationId, CancellationToken ct);
    Task<AssistantExecution> SaveAssistantAsync(long tenantId, long userId, AssistantCommand command, string correlationId, CancellationToken ct);
}

public interface IGovernancaOperacionalService
{
    Task<GovernanceSummary> SummaryAsync(long tenantId, long? userId, CancellationToken ct);
    Task<Page<OperationalItem>> PendenciesAsync(long tenantId, OperationFilter filter, CancellationToken ct);
    Task<IReadOnlyList<ExecutiveMetric>> IndicatorsAsync(long tenantId, string? module, CancellationToken ct);
}

public sealed class GovernancaOperacionalService(IExecutiveOperationsRepository repository) : IGovernancaOperacionalService
{
    public Task<GovernanceSummary> SummaryAsync(long tenantId, long? userId, CancellationToken ct) => repository.GovernanceAsync(tenantId, userId, ct);
    public Task<Page<OperationalItem>> PendenciesAsync(long tenantId, OperationFilter filter, CancellationToken ct) => repository.PendenciesAsync(tenantId, Normalize(filter), ct);
    public Task<IReadOnlyList<ExecutiveMetric>> IndicatorsAsync(long tenantId, string? module, CancellationToken ct) => repository.IndicatorsAsync(tenantId, module, ct);
    private static OperationFilter Normalize(OperationFilter f) => f with { Page = Math.Max(1, f.Page), PageSize = Math.Clamp(f.PageSize, 1, 100) };
}
