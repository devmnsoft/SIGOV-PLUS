using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class DatabaseMigrationRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string MigrationsPath = Path.Combine(Root, "database", "postgres", "migrations");
    private static readonly string[] ForbiddenSchemas =
    {
        "core", "sec", "audit", "lgpd", "fin", "trib", "compras", "rh", "educacao", "saude", "saneamento", "social", "suporte", "operacao", "integracao", "bi", "transparencia"
    };

    [Fact]
    public void Migrations_Devem_Usar_Apenas_Schema_Sigov_E_Metadata_Em_Sigov()
    {
        var allSql = ReadAllMigrations();

        allSql.Should().Contain("create schema if not exists sigov;");
        allSql.Should().Contain("sigov.schema_migrations");
        foreach (var schema in ForbiddenSchemas)
        {
            Regex.IsMatch(allSql, $@"\bcreate\s+schema\s+(if\s+not\s+exists\s+)?{Regex.Escape(schema)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Should().BeFalse($"o schema físico proibido {schema} não deve ser criado");
        }
    }

    [Theory]
    [InlineData("nvarchar")]
    [InlineData("datetime2")]
    [InlineData("bit")]
    [InlineData("uniqueidentifier")]
    [InlineData("rowversion")]
    public void Migrations_Nao_Devem_Usar_Tipos_Do_SQL_Server(string forbiddenType)
    {
        ReadAllMigrations().Should().NotMatchRegex($@"\b{Regex.Escape(forbiddenType)}\b");
    }

    [Fact]
    public void Migrations_Operacionais_Devem_Declarar_TenantId_E_Qualificar_Tabelas_Com_Sigov()
    {
        var sql = ReadAllMigrations();

        sql.Should().Contain("tenant_id");
        var tableNames = Regex.Matches(sql, @"create\s+table\s+(?:if\s+not\s+exists\s+)?(?<name>[a-zA-Z0-9_.%]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .ToArray();

        tableNames.Should().OnlyContain(table => table.StartsWith("sigov.", StringComparison.OrdinalIgnoreCase), "toda tabela criada pelas migrations deve ser qualificada como sigov.<tabela>");
    }

    [Fact]
    public void Manifest_Deve_Estar_Ordenado_Com_Checksums_Normalizados_E_Historico_LicitaPro()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MigrationsPath, "manifest.json")));
        var entries = document.RootElement.GetProperty("migrations").EnumerateArray().ToArray();
        entries.Select(entry => entry.GetProperty("version").GetString()).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

        foreach (var entry in entries.Where(entry => entry.GetProperty("applyAutomatically").GetBoolean()))
        {
            var contents = File.ReadAllText(Path.Combine(MigrationsPath, entry.GetProperty("file").GetString()!))
                .TrimStart('\uFEFF').Replace("\r\n", "\n").Replace("\r", "\n");
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant()
                .Should().Be(entry.GetProperty("checksum").GetString());
        }

        var historical = entries.Single(entry => entry.GetProperty("version").GetString() == "20260903130000");
        historical.GetProperty("knownChecksums").EnumerateArray().Select(value => value.GetString())
            .Should().Contain("2ee4b77413f755230ad1bdaef456893c1f5f045866ea436e78d388a0b4f18364");
    }

    [Fact]
    public void Correcao_LicitaPro_Deve_Ser_Aditiva_Idempotente_E_Validar_Catalogos_Fortes()
    {
        var sql = File.ReadAllText(Path.Combine(MigrationsPath, "20260903173000_corr_licitapro_schema_history.sql"));
        sql.Should().Contain("conrelid=to_regclass('sigov.compras_licitapro_fonte')");
        sql.Should().Contain("create index ix_clp_alerta_tenant_status_vencimento");
        sql.Should().NotContain("create index sigov.ix_clp_alerta_tenant_status_vencimento");
        sql.Should().Contain("pg_index").And.Contain("pg_class").And.Contain("pg_attribute");
        sql.Should().Contain("array['tenant_id','entidade_id','status','vencimento_at']::name[]");
        sql.Should().NotContain("concurrently");
    }

    [Fact]
    public void Runner_Deve_Validar_Antes_Do_Ddl_E_Avaliar_PostConditions_Somente_Ao_Final()
    {
        var runner = File.ReadAllText(Path.Combine(Root, "src", "Sigov.Infrastructure", "Persistence", "Migrations", "MigrationRunner.cs"));
        runner.IndexOf("ThrowIfInvalid(validation);", StringComparison.Ordinal).Should()
            .BeLessThan(runner.IndexOf("EnsureMigrationHistoryAsync(connection", StringComparison.Ordinal));
        runner.IndexOf("// Fase 2:", StringComparison.Ordinal).Should()
            .BeLessThan(runner.IndexOf("// Fase 3:", StringComparison.Ordinal));
        runner.Should().Contain("if (!validateOnly)");
        runner.Should().Contain("history = await ReadMigrationHistoryAsync");
        runner.Should().Contain("pendentes=0; checksum=0; falhas=0");
    }

    private static string ReadAllMigrations() => string.Join('\n', Directory.GetFiles(MigrationsPath, "*.sql", SearchOption.TopDirectoryOnly).OrderBy(static file => file, StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "sigov.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório sigov não encontrada.");
    }
}
