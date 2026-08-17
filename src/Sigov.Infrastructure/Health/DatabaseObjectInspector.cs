using Dapper;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Health;

public sealed class DatabaseObjectInspector : IDatabaseObjectInspector
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public DatabaseObjectInspector(NpgsqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<bool> TableExistsAsync(string schema, string table, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema, nameof(schema));
        ValidateIdentifier(table, nameof(table));
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = "select exists(select 1 from information_schema.tables where table_schema=@Schema and table_name=@Table);";
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { Schema = schema, Table = table }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int?> CountRowsAsync(string schema, string table, CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(schema, table, cancellationToken).ConfigureAwait(false)) return null;
        ValidateIdentifier(schema, nameof(schema));
        ValidateIdentifier(table, nameof(table));
        if (!string.Equals(schema, "sigov", StringComparison.Ordinal) || !string.Equals(table, "schema_migrations", StringComparison.Ordinal))
            throw new NotSupportedException("Contagem não habilitada para este objeto de banco.");
        await using var connection = _connectionFactory.CreateConnection();
        const string sql = "select count(*)::int from sigov.schema_migrations;";
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("Identificador de banco inválido.", parameterName);
    }
}
