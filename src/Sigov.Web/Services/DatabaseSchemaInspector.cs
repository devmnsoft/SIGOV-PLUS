using System.Collections.Concurrent;
using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Web.Services;

public interface IDatabaseSchemaInspector
{
    Task<bool> TableExistsAsync(string schema, string table, CancellationToken cancellationToken);
    Task<bool> ColumnExistsAsync(string schema, string table, string column, CancellationToken cancellationToken);
    Task<IReadOnlySet<string>> GetColumnsAsync(string schema, string table, CancellationToken cancellationToken);
}

public sealed class DatabaseSchemaInspector : IDatabaseSchemaInspector
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, CacheEntry<IReadOnlySet<string>>> _columnsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheEntry<bool>> _tableCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseSchemaInspector> _logger;

    public DatabaseSchemaInspector(NpgsqlConnectionFactory connectionFactory, ILogger<DatabaseSchemaInspector> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<bool> TableExistsAsync(string schema, string table, CancellationToken cancellationToken)
    {
        var key = $"{schema}.{table}";
        if (_tableCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow) return cached.Value;
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"select exists(select 1 from information_schema.tables where table_schema=@Schema and table_name=@Table);";
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { Schema = schema, Table = table }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            _tableCache[key] = new CacheEntry<bool>(exists, DateTimeOffset.UtcNow.Add(CacheTtl));
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao inspecionar existência da tabela {Schema}.{Table}; usando fallback seguro.", schema, table);
            return false;
        }
    }

    public async Task<bool> ColumnExistsAsync(string schema, string table, string column, CancellationToken cancellationToken)
    {
        var columns = await GetColumnsAsync(schema, table, cancellationToken).ConfigureAwait(false);
        return columns.Contains(column);
    }

    public async Task<IReadOnlySet<string>> GetColumnsAsync(string schema, string table, CancellationToken cancellationToken)
    {
        var key = $"{schema}.{table}";
        if (_columnsCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow) return cached.Value;
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"select column_name from information_schema.columns where table_schema=@Schema and table_name=@Table order by ordinal_position;";
            var columns = await connection.QueryAsync<string>(new CommandDefinition(sql, new { Schema = schema, Table = table }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            var set = columns.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _columnsCache[key] = new CacheEntry<IReadOnlySet<string>>(set, DateTimeOffset.UtcNow.Add(CacheTtl));
            _tableCache[key] = new CacheEntry<bool>(set.Count > 0, DateTimeOffset.UtcNow.Add(CacheTtl));
            return set;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao inspecionar colunas de {Schema}.{Table}; usando fallback seguro.", schema, table);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record CacheEntry<T>(T Value, DateTimeOffset ExpiresAt);
}
