using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Migrations;

public sealed class MigrationRunner
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly string _migrationsPath;

    public MigrationRunner(NpgsqlConnectionFactory connectionFactory, IConfiguration configuration, ILogger<MigrationRunner> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        var configuredPath = configuration["Sigov:Database:MigrationsPath"];
        _migrationsPath = ResolveMigrationsPath(configuredPath);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_migrationsPath))
            {
                _logger.LogWarning("Diretório de migrations sigov não encontrado: {MigrationsPath}", _migrationsPath);
                return;
            }

            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("create schema if not exists sigov;", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(@"create table if not exists sigov.schema_migrations (
    id bigserial primary key,
    version varchar(50) not null unique,
    description varchar(250) not null,
    checksum varchar(128) not null,
    applied_at timestamptz not null default now()
);
", cancellationToken: cancellationToken)).ConfigureAwait(false);

            var files = Directory.GetFiles(_migrationsPath, "*.sql").OrderBy(static file => file, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var version = Path.GetFileNameWithoutExtension(file).Split('_', 2)[0];
                var description = Path.GetFileNameWithoutExtension(file);
                var sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var checksum = Checksum(sql);

                var alreadyApplied = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "select exists (select 1 from sigov.schema_migrations where version = @Version);",
                    new { Version = version }, cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (alreadyApplied)
                {
                    continue;
                }

                _logger.LogInformation("Aplicando migration sigov {Version}: {Description}", version, description);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.schema_migrations (version, description, checksum)
values (@Version, @Description, @Checksum);
", new { Version = version, Description = description, Checksum = checksum }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar migrations no schema sigov. CorrelationId={CorrelationId}", Guid.Empty);
            throw;
        }
    }

    private static string ResolveMigrationsPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var fromCurrentDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
            if (Directory.Exists(fromCurrentDirectory))
            {
                return fromCurrentDirectory;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "database", "postgres", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(configuredPath ?? "database/postgres/migrations");
    }

    private static string Checksum(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
