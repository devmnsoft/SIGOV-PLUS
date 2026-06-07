using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class TenantIsolationFullRegressionTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Theory]
    [InlineData("src/Sigov.Infrastructure/Persistence/Repositories/PessoaRepository.cs", "pessoa")]
    [InlineData("src/Sigov.Infrastructure/Persistence/Repositories/UsuarioRepository.cs", "usuario")]
    [InlineData("src/Sigov.Infrastructure/Processos/ProcessosRepositories.cs", "processos")]
    [InlineData("src/Sigov.Infrastructure/Financeiro/FinanceiroRepositories.cs", "financeiro")]
    [InlineData("src/Sigov.Infrastructure/Rh/RhRepository.cs", "rh")]
    [InlineData("src/Sigov.Infrastructure/Educacao/EducacaoRepository.cs", "educacao")]
    [InlineData("src/Sigov.Infrastructure/Saude/SaudeRepository.cs", "saude")]
    [InlineData("src/Sigov.Infrastructure/Saneamento/SaneamentoRepository.cs", "saneamento")]
    [InlineData("src/Sigov.Infrastructure/Social/SocialRepository.cs", "social")]
    [InlineData("src/Sigov.Infrastructure/Integracoes/IntegracaoRepositories.cs", "integracoes")]
    public void Repositories_De_Modulos_Existentes_Devem_Filtrar_TenantId(string relativePath, string moduleName)
    {
        var file = Path.Combine(Root, relativePath);
        if (!File.Exists(file))
        {
            return;
        }

        var source = File.ReadAllText(file);
        var hasSqlTenantGuard = source.Contains("tenant_id", StringComparison.OrdinalIgnoreCase) && source.Contains("@TenantId", StringComparison.Ordinal);
        var hasContextTenantGuard = source.Contains("SocialContexto", StringComparison.Ordinal) || source.Contains("long t", StringComparison.Ordinal);
        (hasSqlTenantGuard || hasContextTenantGuard).Should().BeTrue($"o módulo {moduleName} deve manter isolamento SaaS por tenant_id ou contexto tenant explícito");
    }

    [Fact]
    public void Migrations_De_Modulos_Existentes_Devem_Ter_TenantId_Nas_Tabelas_Operacionais()
    {
        var migrations = Directory.GetFiles(Path.Combine(Root, "database", "postgres", "migrations"), "*.sql", SearchOption.TopDirectoryOnly)
            .Where(static file => Path.GetFileName(file).Any(char.IsDigit))
            .Select(File.ReadAllText);
        var sql = string.Join('\n', migrations);

        sql.Should().Contain("tenant_id bigint not null");
        sql.Should().Contain("tenant_id");
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
