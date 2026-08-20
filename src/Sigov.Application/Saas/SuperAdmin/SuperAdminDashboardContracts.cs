namespace Sigov.Application.Saas.SuperAdmin;

public sealed record SuperAdminDashboardFilter(long? TenantId, DateTimeOffset FromUtc, DateTimeOffset ToUtc, string? Module, string? Status);
public sealed record SuperAdminDashboardKpi(string Code, string Label, long Value, string Status);
public sealed record SuperAdminDashboardAlert(string Area, string Severity, string Message, long? TenantId = null);
public sealed record SuperAdminTenantSummary(long Id, string Name, string Status, string? Plan, int Modules, int Domains, int Entities, int FiscalYears, int Units, bool ContextComplete);
public sealed record SuperAdminAuthorizationSummary(DateTimeOffset AtUtc, long? TenantId, string Resource, string Action, bool Allowed, bool ExplicitDeny, string Reason);
public sealed record SuperAdminAuditSummary(DateTimeOffset AtUtc, long? TenantId, string Area, string Event, string Result, bool Sensitive);
public sealed record SuperAdminContextSummary(DateTimeOffset AtUtc, long? TenantId, string Result, string? Reason);
public sealed record SuperAdminOperationSummary(string Area, string Status, long Count, DateTimeOffset? LastAtUtc);

public sealed record SuperAdminOperationalDashboard(
    DateTimeOffset CheckedAtUtc,
    string DatabaseStatus,
    string SchemaStatus,
    IReadOnlyList<SuperAdminDashboardKpi> Kpis,
    IReadOnlyList<SuperAdminDashboardAlert> Alerts,
    IReadOnlyList<SuperAdminTenantSummary> Tenants,
    IReadOnlyList<SuperAdminAuthorizationSummary> Authorizations,
    IReadOnlyList<SuperAdminAuditSummary> Audits,
    IReadOnlyList<SuperAdminContextSummary> ContextEvents,
    IReadOnlyList<SuperAdminOperationSummary> Operations);

public interface ISuperAdminOperationalDashboardService
{
    Task<SuperAdminOperationalDashboard> GetAsync(SuperAdminDashboardFilter filter, CancellationToken cancellationToken = default);
}
