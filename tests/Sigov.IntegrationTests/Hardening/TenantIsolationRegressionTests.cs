using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests.Hardening;

public sealed class TenantIsolationRegressionTests
{
    [Fact]
    public void Migrations_Nao_Devem_Criar_Schemas_Operacionais_Fora_De_Sigov()
    {
        var root = FindRepositoryRoot();
        var migrationFiles = Directory.GetFiles(Path.Combine(root, "database", "postgres", "migrations"), "*.sql", SearchOption.TopDirectoryOnly);
        var forbiddenSchemas = new[] { "core", "sec", "audit", "lgpd", "fin", "trib", "rh", "educacao", "saude", "saneamento", "social", "suporte", "operacao", "integracao" };

        var offenders = migrationFiles
            .SelectMany(file => forbiddenSchemas.Where(schema => File.ReadAllText(file).Contains($"create schema {schema}", StringComparison.OrdinalIgnoreCase)).Select(schema => new { file, schema }))
            .ToList();

        offenders.Should().BeEmpty("o SaaS sigov usa schema único sigov e tenant_id para isolamento lógico");
    }

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
