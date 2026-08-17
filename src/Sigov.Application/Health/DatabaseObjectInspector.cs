namespace Sigov.Application.Health;

public interface IDatabaseObjectInspector
{
    Task<bool> TableExistsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<int?> CountRowsAsync(string schema, string table, CancellationToken cancellationToken = default);
}
