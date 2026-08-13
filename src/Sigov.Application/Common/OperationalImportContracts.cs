namespace Sigov.Application.Common;

public sealed record OperationalCsvImportRequest(string Csv, char Delimiter = ';');
public sealed record OperationalImportIssue(int Line, string Field, string Error, string Severity);
public sealed record OperationalImportRow(int Line, IReadOnlyDictionary<string, string> Values, bool Valid, IReadOnlyCollection<OperationalImportIssue> Issues);
public sealed record OperationalImportPreview(string Module, string Resource, int Total, int Valid, int Invalid, IReadOnlyCollection<OperationalImportRow> Rows);
public sealed record OperationalImportConfirmation(long ReportId, int Persisted, int Rejected, IReadOnlyCollection<OperationalImportIssue> Issues);

public interface IOperationalImportStore
{
    Task<long> SaveReportAsync(long tenantId, string module, string resource, int total, int persisted, int rejected, object detail, long? userId, string correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<OperationalAlertResponse>> ListAlertsAsync(long tenantId, string? module, string? severity, CancellationToken ct);
    Task<bool> ResolveAlertAsync(long tenantId, long id, long? userId, string justification, string correlationId, CancellationToken ct);
}
public sealed record OperationalAlertResponse(long Id, string Module, string Type, string Severity, string Title, string Description, string Status, DateTimeOffset CreatedAt);
public sealed record ResolveOperationalAlertRequest(string Justification);
