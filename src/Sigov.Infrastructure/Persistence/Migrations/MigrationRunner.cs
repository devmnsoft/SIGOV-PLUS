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
    id bigint generated always as identity primary key,
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
            var versionType = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("select data_type from information_schema.columns where table_schema='sigov' and table_name='schema_migrations' and column_name='version';", cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!string.Equals(versionType, "character varying", StringComparison.OrdinalIgnoreCase) && !string.Equals(versionType, "text", StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition("alter table sigov.schema_migrations alter column version type varchar(50) using version::text;", cancellationToken: cancellationToken)).ConfigureAwait(false);
                _logger.LogInformation("Coluna schema_migrations.version convertida de {PreviousType} para a versão completa textual do manifest.", versionType);
            }

            var migrations = LoadManifestFiles();
            var validateOnly = string.Equals(migrationMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
            var validation = new MigrationValidationResult();
            foreach (var migration in migrations)
            {
                var file = migration.FilePath;
                var version = migration.Version;
                var description = migration.Description;
                var category = migration.Category;
                var expectedChecksum = migration.Checksum;
                var sql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var checksum = Checksum(sql);
                if (!string.Equals(expectedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    validation.ChecksumMismatch.Add(version);
                    _logger.LogError("Checksum divergente no manifest para migration sigov {Version}. Manifest={ExpectedChecksum}; Arquivo={Checksum}", version, expectedChecksum, checksum);
                    continue;
                }

                var alreadyApplied = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "select exists (select 1 from sigov.schema_migrations where version = @Version);",
                    new { Version = version }, cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (alreadyApplied)
                {
                    var storedChecksum = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("select checksum from sigov.schema_migrations where version = @Version;", new { Version = version }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                    if (!string.Equals(storedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        var isKnownHistoricalChecksum = migration.KnownChecksums.Contains(storedChecksum ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                        if (!isKnownHistoricalChecksum)
                        {
                            validation.ChecksumMismatch.Add(version);
                            _logger.LogError("Checksum desconhecido para migration sigov {Version}. Banco={StoredChecksum}; Arquivo={Checksum}", version, storedChecksum, checksum);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(migration.PostConditionSql))
                        {
                            validation.ChecksumMismatch.Add(version);
                            _logger.LogError("Migration sigov {Version} declarou checksum histórico sem postConditionSql.", version);
                            continue;
                        }

                        var postConditionPassed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                            migration.PostConditionSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
                        if (!postConditionPassed)
                        {
                            validation.Failed.Add(version);
                            _logger.LogError("Pós-condição reprovada para checksum histórico da migration sigov {Version}. Banco={StoredChecksum}", version, storedChecksum);
                            continue;
                        }

                        _logger.LogWarning(
                            "Checksum histórico conhecido aceito para migration sigov {Version}, após pós-condição aprovada. Banco={StoredChecksum}; Atual={Checksum}. O checksum armazenado foi preservado.",
                            version, storedChecksum, checksum);
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
values (@Version, @Description, @Checksum, @Category, 'manifest', true, @ExecutionMs);
", new { Version = version, Description = description, Checksum = checksum, Category = category, ExecutionMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                validation.Applied.Add(version);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!validation.IsValid)
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

    private IReadOnlyList<ManifestMigration> LoadManifestFiles()
    {
        if (!File.Exists(_manifestPath))
        {
            throw new FileNotFoundException("manifest.json de migrations não encontrado.", _manifestPath);
        }

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_manifestPath));
        var basePath = Path.GetDirectoryName(_manifestPath) ?? string.Empty;
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ManifestMigration>();
        foreach (var item in document.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var version = item.GetProperty("version").GetString() ?? string.Empty;
            var file = item.GetProperty("file").GetString() ?? string.Empty;
            var description = item.GetProperty("description").GetString() ?? Path.GetFileNameWithoutExtension(file);
            var category = item.GetProperty("category").GetString() ?? "schema";
            var checksum = item.GetProperty("checksum").GetString() ?? string.Empty;
            var knownChecksums = item.TryGetProperty("knownChecksums", out var known) && known.ValueKind == System.Text.Json.JsonValueKind.Array
                ? known.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                : Array.Empty<string>();
            var postConditionSql = item.TryGetProperty("postConditionSql", out var postCondition) && postCondition.ValueKind == System.Text.Json.JsonValueKind.String
                ? postCondition.GetString()
                : null;
            if (!versions.Add(version))
            {
                throw new InvalidOperationException($"Versão duplicada no manifest de migrations: {version}.");
            }

            if (!files.Add(file))
            {
                throw new InvalidOperationException($"Arquivo duplicado no manifest de migrations: {file}.");
            }

            if (item.TryGetProperty("applyAutomatically", out var apply) && apply.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                var filePath = Path.Combine(basePath, file);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Migration automática ausente no manifest.", filePath);
                }

                result.Add(new ManifestMigration(version, filePath, description, category, checksum, knownChecksums, postConditionSql));
            }
        }

        return result;
    }

    private sealed record ManifestMigration(
        string Version,
        string FilePath,
        string Description,
        string Category,
        string Checksum,
        IReadOnlyList<string> KnownChecksums,
        string? PostConditionSql);

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
