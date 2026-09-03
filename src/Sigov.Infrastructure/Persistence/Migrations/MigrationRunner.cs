using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
    public List<string> ChecksumReports { get; } = new();
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
        var activeStage = "History";
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
                activeStage = "History";
                await EnsureMigrationHistoryAsync(connection, cancellationToken).ConfigureAwait(false);

                var manifest = LoadManifestFiles();
                var validateOnly = string.Equals(migrationMode, "ValidateOnly", StringComparison.OrdinalIgnoreCase);
                var validation = new MigrationValidationResult();
                foreach (var migration in manifest.Migrations)
                {
                    activeVersion = migration.Version;
                    activeMigrationFile = Path.GetFileName(migration.FilePath);
                    activeStage = "History";
                    var file = migration.FilePath;
                    var version = migration.Version;
                    var description = migration.Description;
                    var category = migration.Category;
                    var expectedChecksum = migration.Checksum;
                    var rawSql = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    var checksum = Checksum(rawSql);
                    var storedChecksum = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                        "select checksum from sigov.schema_migrations where version = @Version;",
                        new { Version = version }, cancellationToken: cancellationToken)).ConfigureAwait(false);
                    var alreadyApplied = storedChecksum is not null;
                    if (!string.Equals(expectedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        validation.ChecksumMismatch.Add(version);
                        var report = FormatChecksumReport(migration, checksum, storedChecksum, false, null,
                            "MANIFEST_OUTDATED: o checksum do arquivo normalizado difere do manifest; verifique alteração de conteúdo, encoding ou fim de linha.");
                        validation.ChecksumReports.Add(report);
                        _logger.LogError("{ChecksumReport}", report);
                        continue;
                    }

                    var executionSql = MigrationSqlPolicy.PrepareForExecution(version, rawSql, migration.LegacyTransactionWrapper);

                    if (alreadyApplied)
                    {
                        if (!string.Equals(storedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                        {
                            var isKnownHistoricalChecksum = migration.KnownChecksums.Contains(storedChecksum ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                            if (!isKnownHistoricalChecksum)
                            {
                                validation.ChecksumMismatch.Add(version);
                                var report = FormatChecksumReport(migration, checksum, storedChecksum, false, null,
                                    "DATABASE_HISTORY_INCONSISTENT: checksum armazenado não corresponde ao atual nem consta em knownChecksums.");
                                validation.ChecksumReports.Add(report);
                                _logger.LogError("{ChecksumReport}", report);
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(migration.PostConditionSql))
                            {
                                validation.ChecksumMismatch.Add(version);
                                var report = FormatChecksumReport(migration, checksum, storedChecksum, true, null,
                                    "POSTCONDITION_MISSING: checksum histórico conhecido exige postConditionSql forte.");
                                validation.ChecksumReports.Add(report);
                                _logger.LogError("{ChecksumReport}", report);
                                continue;
                            }

                            activeStage = "PostCondition";
                            var postConditionPassed = await EvaluatePostConditionAsync(connection, migration, null, cancellationToken).ConfigureAwait(false);
                            if (!postConditionPassed)
                            {
                                validation.Failed.Add(version);
                                validation.ChecksumReports.Add(FormatPostConditionReport(migration, false,
                                    await FindMissingExpectedObjectsAsync(connection, migration.PostConditionSql, cancellationToken).ConfigureAwait(false)));
                                var report = FormatChecksumReport(migration, checksum, storedChecksum, true, false,
                                    "POSTCONDITION_FAILED: o estado atual do banco não comprova a migration histórica.");
                                validation.ChecksumReports.Add(report);
                                _logger.LogError("{ChecksumReport}", report);
                                continue;
                            }

                            _logger.LogWarning(
                                "Checksum histórico conhecido aceito para migration sigov {Version}, após pós-condição aprovada. Banco={StoredChecksum}; Atual={Checksum}. O checksum armazenado foi preservado.",
                                version, storedChecksum, checksum);
                        }
                        else if (!string.IsNullOrWhiteSpace(migration.PostConditionSql))
                        {
                            activeStage = "PostCondition";
                            var postConditionPassed = await EvaluatePostConditionAsync(connection, migration, null, cancellationToken).ConfigureAwait(false);
                            if (!postConditionPassed)
                            {
                                validation.Failed.Add(version);
                                var report = FormatPostConditionReport(migration, false, await FindMissingExpectedObjectsAsync(connection, migration.PostConditionSql, cancellationToken).ConfigureAwait(false));
                                validation.ChecksumReports.Add(report);
                                _logger.LogError("{PostConditionReport}", report);
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

                    await ApplyMigrationAsync(connection, migration, executionSql, checksum, (stage, file) => { activeStage = stage; activeCompatibilityFile = file; }, cancellationToken).ConfigureAwait(false);
                    activeCompatibilityFile = null;
                    validation.Applied.Add(version);
                }

                if (!validation.IsValid)
                {
                    var details = validation.ChecksumReports.Count == 0
                        ? string.Empty
                        : Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, validation.ChecksumReports);
                    throw new InvalidOperationException($"Validação de migrations falhou: pendentes={validation.Pending.Count}; checksum={validation.ChecksumMismatch.Count}; falhas={validation.Failed.Count}.{details}");
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
            _logger.LogError(ex, "{OperationalHint} Migration={Migration}; MigrationFile={MigrationFile}; Stage={Stage}; CompatibilityFile={CompatibilityFile}; SqlState={SqlState}; ColumnName={ColumnName}; TableName={TableName}; SchemaName={SchemaName}; ConstraintName={ConstraintName}; Routine={Routine}; Detail={Detail}; Hint={Hint}; Position={Position}; DatabaseUser={DatabaseUser}; CorrelationId={CorrelationId}",
                hint, activeVersion, activeMigrationFile, activeStage, activeCompatibilityFile, ex.SqlState, ex.ColumnName, ex.TableName, ex.SchemaName, ex.ConstraintName, ex.Routine, ex.Detail, ex.Hint, ex.Position, connection.UserName, System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"));
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

    private async Task ApplyMigrationAsync(NpgsqlConnection connection, ManifestMigration migration, string executionSql, string checksum, Action<string, string?> setActiveStage, CancellationToken cancellationToken)
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
                setActiveStage("CompatibilityBefore", compatibility.File);
                _logger.LogInformation("Compatibility PRE: {CompatibilityFile}; Migration={Version}; Status=Started; CorrelationId={CorrelationId}", compatibility.File, migration.Version, correlationId);
                var compatibilitySql = await File.ReadAllTextAsync(compatibility.FilePath, cancellationToken).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(compatibilitySql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                _logger.LogInformation("Compatibility applied: {CompatibilityFile}; Migration={Version}; CorrelationId={CorrelationId}", compatibility.File, migration.Version, correlationId);
            }
            setActiveStage("Migration", null);
            await connection.ExecuteAsync(new CommandDefinition(executionSql, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            _logger.LogInformation("Migration applied: {Version}; File={MigrationFile}; CorrelationId={CorrelationId}", migration.Version, Path.GetFileName(migration.FilePath), correlationId);
            if (!string.IsNullOrWhiteSpace(migration.PostConditionSql))
            {
                setActiveStage("PostCondition", null);
                var postConditionPassed = await EvaluatePostConditionAsync(connection, migration, transaction, cancellationToken).ConfigureAwait(false);
                if (!postConditionPassed)
                {
                    var report = FormatPostConditionReport(migration, false, await FindMissingExpectedObjectsAsync(connection, migration.PostConditionSql, cancellationToken, transaction).ConfigureAwait(false));
                    _logger.LogError("{PostConditionReport}", report);
                    throw new InvalidOperationException($"Pós-condição reprovada após aplicar migration {migration.Version}.{Environment.NewLine}{report}");
                }
            }

            var executionMs = stopwatch.ElapsedMilliseconds;
            setActiveStage("History", null);
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
        var normalized = value.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace("\r", "\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private async Task<bool> EvaluatePostConditionAsync(NpgsqlConnection connection, ManifestMigration migration, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        var commandText = migration.PostConditionSql;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            const string reason = "commandText da postcondition ausente, nulo ou vazio";
            _logger.LogError(
                "Postcondition inválida. Migration={Version}; File={MigrationFile}; PostCondition={PostCondition}; Description={Description}; Reason={Reason}",
                migration.Version,
                Path.GetFileName(migration.FilePath),
                "postConditionSql",
                migration.Description,
                reason);
            throw new InvalidOperationException(
                $"Postcondition inválida para a migration {migration.Version} " +
                $"(arquivo {Path.GetFileName(migration.FilePath)}, descrição '{migration.Description}', postcondition 'postConditionSql'): {reason}.");
        }

        var obtained = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            commandText, transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (!obtained)
        {
            var missing = await FindMissingExpectedObjectsAsync(connection, commandText, cancellationToken, transaction).ConfigureAwait(false);
            _logger.LogError("{PostConditionReport}", FormatPostConditionReport(migration, obtained, missing));
        }

        return obtained;
    }

    private static readonly Regex RegclassReference = new(@"to_regclass\s*\(\s*'(?<name>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CatalogReference = new(@"(?<kind>conname|tgname)\s*=\s*'(?<name>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PermissionReference = new(@"\bchave\s*=\s*'(?<name>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ColumnReference = new(@"table_schema\s*=\s*'(?<schema>[^']+)'\s+and\s+(?:\w+\.)?table_name\s*=\s*'(?<table>[^']+)'[\s\S]{0,240}?(?:\w+\.)?column_name\s*=\s*'(?<column>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static async Task<IReadOnlyList<string>> FindMissingExpectedObjectsAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken, NpgsqlTransaction? transaction = null)
    {
        var missing = new List<string>();
        foreach (Match match in RegclassReference.Matches(sql))
        {
            var name = match.Groups["name"].Value;
            // Expressões como to_regclass('sigov.' || required.table_name)
            // não são referências concretas. Sondar "sigov." gerava um falso
            // MissingObjects e escondia o diagnóstico produzido pela própria
            // pós-condição dinâmica.
            if (name.EndsWith(".", StringComparison.Ordinal)) continue;
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select to_regclass(@Name) is not null", new { Name = name }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!exists) missing.Add($"relation:{name}");
        }

        foreach (Match match in CatalogReference.Matches(sql))
        {
            var kind = match.Groups["kind"].Value.ToLowerInvariant();
            var name = match.Groups["name"].Value;
            var query = kind == "conname" ? "select exists(select 1 from pg_constraint where conname=@Name)" : "select exists(select 1 from pg_trigger where tgname=@Name and not tgisinternal)";
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(query, new { Name = name }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!exists) missing.Add($"{(kind == "conname" ? "constraint" : "trigger")}:{name}");
        }

        foreach (Match match in PermissionReference.Matches(sql))
        {
            var name = match.Groups["name"].Value;
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.permissao where chave=@Name)", new { Name = name }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!exists) missing.Add($"permission:{name}");
        }

        foreach (Match match in ColumnReference.Matches(sql))
        {
            var schema = match.Groups["schema"].Value;
            var table = match.Groups["table"].Value;
            var column = match.Groups["column"].Value;
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from information_schema.columns where table_schema=@Schema and table_name=@Table and column_name=@Column)", new { Schema = schema, Table = table, Column = column }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (!exists) missing.Add($"column:{schema}.{table}.{column}");
        }

        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FormatPostConditionReport(ManifestMigration migration, bool obtained, IReadOnlyList<string> missing) =>
        string.Join(Environment.NewLine, new[]
        {
            "Post-condition validation:",
            $"Version={migration.Version}",
            $"Description={migration.Description}",
            $"Identifier={Path.GetFileNameWithoutExtension(migration.FilePath)}:postConditionSql",
            $"Sql={migration.PostConditionSql}",
            "Expected=true",
            $"Obtained={obtained.ToString().ToLowerInvariant()}",
            $"MissingObjects={(missing.Count == 0 ? "not-detectable-by-catalog-probes" : string.Join(",", missing))}",
            $"ExpectedTablesIndexesConstraintsPermissions={string.Join(",", RegclassReference.Matches(migration.PostConditionSql ?? string.Empty).Cast<Match>().Select(match => match.Groups["name"].Value).Where(name => !name.EndsWith(".", StringComparison.Ordinal)).Concat(CatalogReference.Matches(migration.PostConditionSql ?? string.Empty).Cast<Match>().Select(match => match.Groups["name"].Value)).Concat(PermissionReference.Matches(migration.PostConditionSql ?? string.Empty).Cast<Match>().Select(match => match.Groups["name"].Value)).Concat(ColumnReference.Matches(migration.PostConditionSql ?? string.Empty).Cast<Match>().Select(match => $"{match.Groups["schema"].Value}.{match.Groups["table"].Value}.{match.Groups["column"].Value}")).Distinct(StringComparer.OrdinalIgnoreCase))}"
        });

    private static string FormatChecksumReport(
        ManifestMigration migration,
        string fileChecksum,
        string? databaseChecksum,
        bool knownHistorical,
        bool? postConditionPassed,
        string probableCause)
    {
        var postCondition = string.IsNullOrWhiteSpace(migration.PostConditionSql)
            ? "missing"
            : postConditionPassed is null
                ? "not-evaluated"
                : postConditionPassed.Value
                    ? "passed"
                    : "failed";

        return string.Join(Environment.NewLine, new[]
        {
            "Checksum mismatch:",
            $"Version={migration.Version}",
            $"File={Path.GetFileName(migration.FilePath)}",
            $"Description={migration.Description}",
            $"Manifest={migration.Checksum}",
            $"FileActual={fileChecksum}",
            $"DatabaseStored={databaseChecksum ?? "not-applied"}",
            $"KnownHistorical={knownHistorical}",
            $"PostCondition={postCondition}",
            $"ProbableCause={probableCause}"
        });
    }
}
