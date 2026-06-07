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
