using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
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
    private readonly TimeSpan _migrationLockTimeout;
    private const long MigrationLockKey = 0x5349474F56504C55; // "SIGOVPLU", stable per database.

    public MigrationRunner(NpgsqlConnectionFactory connectionFactory, IConfiguration configuration, ILogger<MigrationRunner> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        var configuredPath = configuration["Sigov:Database:MigrationsPath"];
        _migrationsPath = ResolveMigrationsPath(configuredPath);
        _manifestPath = Path.Combine(_migrationsPath, "manifest.json");
        var lockTimeoutSeconds = configuration.GetValue("Sigov:Database:MigrationLockTimeoutSeconds", 60);
        if (lockTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Sigov:Database:MigrationLockTimeoutSeconds deve ser maior que zero.");
        }

        _migrationLockTimeout = TimeSpan.FromSeconds(lockTimeoutSeconds);
    }

    public Task RunAsync(CancellationToken cancellationToken = default) => RunAsync("ApplyPending", cancellationToken);

    public async Task RunAsync(string migrationMode, CancellationToken cancellationToken = default)
    {
        string? activeVersion = null;
        string? activeMigrationFile = null;
        string? activeCompatibilityFile = null;
        if (string.Equals(migrationMode, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("MigrationRunner desabilitado; nenhuma conexão será aberta.");
            return;
        }

        if (!string.Equals(migrationMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(migrationMode, "ApplyPending", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"MigrationMode não suportado: {migrationMode}. Use Disabled, ValidateOnly ou ApplyPending.", nameof(migrationMode));
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
            await AcquireMigrationLockAsync(connection, cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureMigrationHistoryAsync(connection, cancellationToken).ConfigureAwait(false);

                var manifest = LoadManifestFiles();
                var validateOnly = string.Equals(migrationMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
                var validation = new MigrationValidationResult();
                foreach (var migration in manifest.Migrations)
                {
                    activeVersion = migration.Version;
                    activeMigrationFile = Path.GetFileName(migration.FilePath);
                    var file = migration.FilePath;
                    var version = migration.Version;
                    var description = migration.Description;
                    var category = migration.Category;
                    var expectedChecksum = migration.Checksum;
                    var rawSql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    var checksum = Checksum(rawSql);
                    if (!string.Equals(expectedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        validation.ChecksumMismatch.Add(version);
                        _logger.LogError("Checksum divergente no manifest para migration sigov {Version}. Manifest={ExpectedChecksum}; Arquivo={Checksum}", version, expectedChecksum, checksum);
                        continue;
                    }

                    var executionSql = MigrationSqlPolicy.PrepareForExecution(version, rawSql, migration.LegacyTransactionWrapper);

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
                        else if (!string.IsNullOrWhiteSpace(migration.PostConditionSql))
                        {
                            var postConditionPassed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                                migration.PostConditionSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
                            if (!postConditionPassed)
                            {
                                validation.Failed.Add(version);
                                _logger.LogError("Pós-condição reprovada para migration sigov {Version}.", version);
                            }
                        }
                        continue;
                    }

                    if (validateOnly)
                    {
                        validation.Pending.Add(version);
                        _logger.LogError("Migration pendente {Version} detectada em modo ValidateOnly.", version);
                        continue;
                    }

                    await ApplyMigrationAsync(connection, migration, executionSql, checksum, file => activeCompatibilityFile = file, cancellationToken).ConfigureAwait(false);
                    activeCompatibilityFile = null;
                    validation.Applied.Add(version);
                }

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException($"Validação de migrations falhou: pendentes={validation.Pending.Count}; checksum={validation.ChecksumMismatch.Count}; falhas={validation.Failed.Count}.");
                }

                if (!validateOnly && validation.Applied.Count > 0 && manifest.CompatibilityAfterAll.Count > 0)
                {
                    await ApplyCompatibilityAfterAllAsync(connection, manifest.CompatibilityAfterAll, file => activeCompatibilityFile = file, cancellationToken).ConfigureAwait(false);
                    activeCompatibilityFile = null;
                }
            }
            finally
            {
                await ReleaseMigrationLockAsync(connection).ConfigureAwait(false);
            }
        }
        catch (PostgresException ex)
        {
            var connection = _connectionFactory.CreateConnection();
            var hint = ex.SqlState switch
            {
                PostgresErrorCodes.InvalidPassword => $"Senha do usuário PostgreSQL '{connection.UserName}' inválida. Execute ./scripts/provision-sigov-db-user.ps1 e confira ConnectionStrings__DefaultConnection e .env.local.",
                PostgresErrorCodes.InvalidCatalogName => "Banco PostgreSQL não existe. Execute ./scripts/install-sigov-database.ps1.",
                PostgresErrorCodes.InsufficientPrivilege => "Permissão insuficiente para o usuário runtime. Execute ./scripts/provision-sigov-db-user.ps1.",
                PostgresErrorCodes.UndefinedTable => "Tabela obrigatória ausente. Execute diagnose-sigov-database.ps1 e validate-sigov-runtime.ps1.",
                PostgresErrorCodes.UniqueViolation => "Duplicidade encontrada. Execute repair-sigov-database.ps1 e diagnose-sigov-database.ps1.",
                _ => "Falha PostgreSQL durante a validação de migrations; consulte o SQLSTATE e o diagnóstico operacional."
            };
            _logger.LogError(ex, "{OperationalHint} Migration={Migration}; File={MigrationFile}; CompatibilityFile={CompatibilityFile}; Relation={Relation}; SqlState={SqlState}; DatabaseUser={DatabaseUser}; CorrelationId={CorrelationId}",
                hint, activeVersion, activeMigrationFile, activeCompatibilityFile, ex.TableName, ex.SqlState, connection.UserName, System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar migrations no schema sigov. CorrelationId={CorrelationId}",
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"));
            throw;
        }
    }

    private async Task EnsureMigrationHistoryAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
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
    }

    private async Task ApplyMigrationAsync(NpgsqlConnection connection, ManifestMigration migration, string executionSql, string checksum, Action<string?> setActiveCompatibility, CancellationToken cancellationToken)
    {
        var correlationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Aplicando migration {Version}: {Description}. Category={Category}; Status=Started; StartedAt={StartedAt}; CorrelationId={CorrelationId}",
            migration.Version, migration.Description, migration.Category, DateTimeOffset.UtcNow, correlationId);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var compatibility in migration.CompatibilityBefore)
            {
                setActiveCompatibility(compatibility.File);
                _logger.LogInformation("Compatibility PRE: {CompatibilityFile}; Migration={Version}; Status=Started; CorrelationId={CorrelationId}", compatibility.File, migration.Version, correlationId);
                var compatibilitySql = await File.ReadAllTextAsync(compatibility.FilePath, cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(compatibilitySql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                _logger.LogInformation("Compatibility applied: {CompatibilityFile}; Migration={Version}; CorrelationId={CorrelationId}", compatibility.File, migration.Version, correlationId);
            }
            setActiveCompatibility(null);
            await connection.ExecuteAsync(new CommandDefinition(executionSql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            _logger.LogInformation("Migration applied: {Version}; File={MigrationFile}; CorrelationId={CorrelationId}", migration.Version, Path.GetFileName(migration.FilePath), correlationId);
            if (!string.IsNullOrWhiteSpace(migration.PostConditionSql))
            {
                var postConditionPassed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    migration.PostConditionSql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                if (!postConditionPassed)
                {
                    throw new InvalidOperationException($"Pós-condição reprovada após aplicar migration {migration.Version}.");
                }
            }

            var executionMs = stopwatch.ElapsedMilliseconds;
            await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.schema_migrations (version, description, checksum, category, source, success, execution_ms)
values (@Version, @Description, @Checksum, @Category, 'manifest', true, @ExecutionMs);
", new { migration.Version, migration.Description, Checksum = checksum, migration.Category, ExecutionMs = executionMs }, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("History committed: {Version}; File={MigrationFile}; CorrelationId={CorrelationId}", migration.Version, Path.GetFileName(migration.FilePath), correlationId);
            _logger.LogInformation("Migration {Version} aplicada em {ExecutionMs}ms. Category={Category}; Status=Applied; CorrelationId={CorrelationId}",
                migration.Version, stopwatch.ElapsedMilliseconds, migration.Category, correlationId);
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollbackException)
            {
                _logger.LogError(rollbackException, "Rollback da migration {Version} falhou; a conexão será descartada.", migration.Version);
            }
            var sqlState = (ex as PostgresException)?.SqlState;
            _logger.LogError(ex, "Migration {Version} falhou após {ExecutionMs}ms. Category={Category}; Status=Failed; SqlState={SqlState}; CorrelationId={CorrelationId}",
                migration.Version, stopwatch.ElapsedMilliseconds, migration.Category, sqlState, correlationId);
            throw;
        }
    }

    private async Task ApplyCompatibilityAfterAllAsync(NpgsqlConnection connection, IReadOnlyList<CompatibilityScript> scripts, Action<string?> setActiveCompatibility, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var script in scripts)
            {
                setActiveCompatibility(script.File);
                var sql = await File.ReadAllTextAsync(script.FilePath, cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                _logger.LogInformation("Compatibility AFTER ALL applied: {CompatibilityFile}", script.File);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task AcquireMigrationLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _migrationLockTimeout)
        {
            var acquired = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "select pg_try_advisory_lock(@LockKey);", new { LockKey = MigrationLockKey }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (acquired)
            {
                _logger.LogInformation("Lock global de migrations adquirido em {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Não foi possível obter o lock de migrations dentro de {_migrationLockTimeout.TotalSeconds:0} segundos.");
    }

    private async Task ReleaseMigrationLockAsync(NpgsqlConnection connection)
    {
        try
        {
            var released = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "select pg_advisory_unlock(@LockKey);", new { LockKey = MigrationLockKey })).ConfigureAwait(false);
            if (!released)
            {
                _logger.LogWarning("A conexão não possuía o lock global de migrations no momento da liberação.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível liberar explicitamente o lock de migrations; o fechamento da conexão irá liberá-lo.");
        }
    }

    private ManifestDefinition LoadManifestFiles()
    {
        if (!File.Exists(_manifestPath))
        {
            throw new FileNotFoundException("manifest.json de migrations não encontrado.", _manifestPath);
        }

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_manifestPath));
        var basePath = Path.GetDirectoryName(_manifestPath) ?? string.Empty;
        var bootstrapPath = Path.GetFullPath(Path.Combine(basePath, "..", "bootstrap"));
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
            var legacyTransactionWrapper = item.TryGetProperty("legacyTransactionWrapper", out var legacyWrapper) && legacyWrapper.ValueKind == System.Text.Json.JsonValueKind.True;
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

                var compatibilityBefore = ReadCompatibilityScripts(item, "compatibilityBefore", bootstrapPath);
                result.Add(new ManifestMigration(version, filePath, description, category, checksum, knownChecksums, postConditionSql, legacyTransactionWrapper, compatibilityBefore));
            }
        }

        var afterAll = ReadCompatibilityScripts(document.RootElement, "compatibilityAfterAll", bootstrapPath);
        return new ManifestDefinition(result, afterAll);
    }

    private static IReadOnlyList<CompatibilityScript> ReadCompatibilityScripts(System.Text.Json.JsonElement owner, string propertyName, string bootstrapPath)
    {
        if (!owner.TryGetProperty(propertyName, out var scripts) || scripts.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return Array.Empty<CompatibilityScript>();
        }

        var result = new List<CompatibilityScript>();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bootstrapPrefix = bootstrapPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var item in scripts.EnumerateArray())
        {
            var file = item.GetProperty("file").GetString() ?? string.Empty;
            var expectedChecksum = item.GetProperty("checksum").GetString() ?? string.Empty;
            if (Path.IsPathRooted(file) || file.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 || !files.Add(file))
            {
                throw new InvalidOperationException($"Arquivo de compatibilidade inválido ou duplicado: {file}.");
            }

            var filePath = Path.GetFullPath(Path.Combine(bootstrapPath, file));
            if (!filePath.StartsWith(bootstrapPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Arquivo de compatibilidade ausente ou fora de database/postgres/bootstrap.", filePath);
            }

            var actualChecksum = Checksum(File.ReadAllText(filePath));
            if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Checksum divergente para compatibilidade {file}. Manifest={expectedChecksum}; Arquivo={actualChecksum}.");
            }
            result.Add(new CompatibilityScript(file, filePath, expectedChecksum));
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
        string? PostConditionSql,
        bool LegacyTransactionWrapper,
        IReadOnlyList<CompatibilityScript> CompatibilityBefore);

    private sealed record CompatibilityScript(string File, string FilePath, string Checksum);
    private sealed record ManifestDefinition(IReadOnlyList<ManifestMigration> Migrations, IReadOnlyList<CompatibilityScript> CompatibilityAfterAll);

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
