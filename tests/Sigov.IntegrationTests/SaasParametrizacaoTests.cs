using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class SaasParametrizacaoTests
{
    [Fact]
    public void Migration_cria_tabelas_no_schema_sigov_e_nao_cria_schema_saas()
    {
        var sql = ReadMigration();
        sql.Should().Contain("sigov.perfil_nivel");
        sql.Should().Contain("sigov.tenant_modulo_contratado");
        sql.Should().Contain("sigov.tenant_parametro_valor");
        sql.Should().NotContain("create schema saas");
    }

    [Fact]
    public void Seeds_de_niveis_e_pacotes_existem()
    {
        var sql = ReadMigration();
        sql.Should().Contain("ADMINISTRADOR_GERAL");
        sql.Should().Contain("ADMINISTRADOR_TENANT");
        sql.Should().Contain("COMPLETO");
        sql.Should().Contain("ESSENCIAL");
    }

    [Fact]
    public void Migration_reforca_isolamento_por_tenant_e_contexto_global()
    {
        var sql = ReadMigration();
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().Contain("usuario_contexto_global_log");
        sql.Should().Contain("idx_usuario_contexto_global_log_tenant");
    }

    private static string ReadMigration()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "database", "postgres", "migrations", "20260608090000_saas_parametrizacao_perfis_modulos.sql");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        return File.ReadAllText("database/postgres/migrations/20260608090000_saas_parametrizacao_perfis_modulos.sql");
    }
}
