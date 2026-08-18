namespace Sigov.Application.Health;

public interface IDatabaseObjectInspector
{
    Task<bool> TableExistsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<bool> ColumnExistsAsync(string schema, string table, string column, CancellationToken cancellationToken = default);
    Task<int?> CountRowsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<MigrationDiagnostic?> GetLatestMigrationAsync(bool success, CancellationToken cancellationToken = default);
}
