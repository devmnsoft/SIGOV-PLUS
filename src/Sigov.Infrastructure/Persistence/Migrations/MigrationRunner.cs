using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Persistence.Migrations;

public sealed class MigrationValidationResult
{
    public List<string> Applied { get; } = new();
    public List<string> Pending { get; } = new();
    public List<string> Excluded { get; } = new();
    public List<string> ChecksumMismatch { get; } = new();
    public List<string> Failed { get; } = new();
    public bool IsValid => Pending.Count == 0 && ChecksumMismatch.Count == 0 && Failed.Count == 0;
}

public sealed class MigrationRunner
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly string _migrationsPath;
    private readonly string _manifestPath;

    public MigrationRunner(NpgsqlConnectionFactory connectionFactory, IConfiguration configuration, ILogger<MigrationRunner> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        var configuredPath = configuration["Sigov:Database:MigrationsPath"];
        _migrationsPath = ResolveMigrationsPath(configuredPath);
        _manifestPath = Path.Combine(_migrationsPath, "manifest.json");
    }

    public Task RunAsync(CancellationToken cancellationToken = default) => RunAsync("ApplyPending", cancellationToken);

    public async Task RunAsync(string migrationMode, CancellationToken cancellationToken = default)
    {
        if (string.Equals(migrationMode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("MigrationRunner desabilitado; nenhuma conexão será aberta.");
            return;
        }

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
    category varchar(40) not null default 'schema',
    source varchar(40) not null default 'manifest',
    success boolean not null default true,
    execution_ms bigint null,
    applied_at timestamptz not null default now()
);
", cancellationToken: cancellationToken)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition("alter table sigov.schema_migrations add column if not exists category varchar(40) not null default 'schema'; alter table sigov.schema_migrations add column if not exists source varchar(40) not null default 'manifest'; alter table sigov.schema_migrations add column if not exists success boolean not null default true; alter table sigov.schema_migrations add column if not exists execution_ms bigint null;", cancellationToken: cancellationToken)).ConfigureAwait(false);

            var files = LoadManifestFiles();
            var validateOnly = string.Equals(migrationMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
            var validation = new MigrationValidationResult();
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
                    var storedChecksum = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("select checksum from sigov.schema_migrations where version = @Version;", new { Version = version }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                    if (!string.Equals(storedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        validation.ChecksumMismatch.Add(version);
                        _logger.LogError("Checksum divergente para migration sigov {Version}. Banco={StoredChecksum}; Arquivo={Checksum}", version, storedChecksum, checksum);
                    }
                    continue;
                }

                if (validateOnly)
                {
                    validation.Pending.Add(version);
                    _logger.LogError("Migration pendente {Version} detectada em modo ValidateOnly.", version);
                    continue;
                }

                _logger.LogInformation("Aplicando migration sigov {Version}: {Description}", version, description);
                var started = DateTimeOffset.UtcNow;
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.schema_migrations (version, description, checksum, category, source, success, execution_ms)
values (@Version, @Description, @Checksum, 'schema', 'manifest', true, @ExecutionMs);
", new { Version = version, Description = description, Checksum = checksum, ExecutionMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                validation.Applied.Add(version);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (validateOnly && !validation.IsValid)
            {
                throw new InvalidOperationException($"Validação de migrations falhou: pendentes={validation.Pending.Count}; checksum={validation.ChecksumMismatch.Count}; falhas={validation.Failed.Count}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar migrations no schema sigov. CorrelationId={CorrelationId}", Guid.Empty);
            throw;
        }
    }

    private IReadOnlyList<string> LoadManifestFiles()
    {
        if (!File.Exists(_manifestPath))
        {
            throw new FileNotFoundException("manifest.json de migrations não encontrado.", _manifestPath);
        }

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_manifestPath));
        return document.RootElement.GetProperty("migrations").EnumerateArray()
            .Where(static item => item.TryGetProperty("applyAutomatically", out var apply) && apply.ValueKind == System.Text.Json.JsonValueKind.True)
            .Select(item => Path.Combine(Path.GetDirectoryName(_manifestPath) ?? string.Empty, item.GetProperty("file").GetString() ?? string.Empty))
            .Where(File.Exists)
            .ToArray();
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
