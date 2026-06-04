using FluentAssertions;
using Sigov.Infrastructure.Persistence;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class InfrastructureSmokeTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Testcontainers_PostgreSql_Esta_Referenciado()
    {
        typeof(Testcontainers.PostgreSql.PostgreSqlBuilder).FullName.Should().Contain("PostgreSqlBuilder");
    }

    [Fact]
    public void DbSchema_Deve_Ser_Sigov()
    {
        DbSchema.Name.Should().Be("sigov");
    }

    [Fact]
    public void Migrations_Devem_Criar_Schema_E_Registrar_Em_Sigov_Schema_Migrations()
    {
        var firstMigration = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/001_create_sigov_schema.sql"));

        firstMigration.Should().Contain("create schema if not exists sigov;");
        firstMigration.Should().Contain("create table if not exists sigov.schema_migrations");
    }

    [Fact]
    public void Migrations_Nao_Devem_Criar_Schemas_Legados()
    {
        var sql = ReadAllMigrations().ToLowerInvariant();
        var legacySchemas = new[] { "core", "sec", "audit", "lgpd", "workflow", "bi", "fin", "trib", "compras", "rh", "educ", "saude", "social", "san", "geo", "suporte", "integracao", "config" };

        foreach (var schema in legacySchemas)
        {
            sql.Should().NotContain($"create schema if not exists {schema}");
            sql.Should().NotContain($"create schema {schema}");
        }
    }

    [Fact]
    public void Repositories_Devem_Usar_Tabelas_Qualificadas_No_Schema_Sigov()
    {
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/PessoaRepository.cs")).Should().Contain("from sigov.pessoa");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/UsuarioRepository.cs")).Should().Contain("from sigov.usuario");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/AuditRepository.cs")).Should().Contain("from sigov.trilha_auditoria");
    }

    [Fact]
    public void Health_Db_Deve_Consultar_Schema_Sigov()
    {
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/HealthController.cs")).Should().Contain("schema_name = 'sigov'");
    }

    [Fact]
    public void Seed_Deve_Criar_Admin_De_Desenvolvimento()
    {
        var seed = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/011_seed_sigov_dev.sql"));
        seed.Should().Contain("admin@sigov.local");
        seed.Should().Contain("SIGOV_ADMIN_PASSWORD");
        seed.Should().Contain("DEV_ONLY:");
    }

    [Fact]
    public void Saas_Migrations_Devem_Criar_Tenants_E_Isolamento()
    {
        var sql = ReadAllMigrations().ToLowerInvariant();

        sql.Should().Contain("create table if not exists sigov.tenant");
        sql.Should().Contain("create table if not exists sigov.tenant_assinatura");
        sql.Should().Contain("create table if not exists sigov.tenant_modulo");
        sql.Should().Contain("create or replace function sigov.current_tenant_id()");
        sql.Should().Contain("enable row level security");
    }

    [Fact]
    public void Repositories_Criticos_Devem_Filtrar_Por_TenantId()
    {
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/PessoaRepository.cs")).Should().Contain("where tenant_id = @TenantId");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/UsuarioRepository.cs")).Should().Contain("where tenant_id = @TenantId");
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Persistence/Repositories/AuditRepository.cs")).Should().Contain("where tenant_id = @TenantId");
    }

    private static string ReadAllMigrations()
    {
        return string.Join('\n', Directory.GetFiles(Path.Combine(Root, "database/postgres/migrations"), "*.sql").Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
