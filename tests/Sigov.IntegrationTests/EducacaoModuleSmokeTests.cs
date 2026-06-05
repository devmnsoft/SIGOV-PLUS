using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class EducacaoModuleSmokeTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Migration_Cria_Tabelas_E_Views_No_Schema_Sigov_Com_Tenant()
    {
        var sql = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/021_educacao_base.sql")).ToLowerInvariant();
        foreach (var table in new[] { "escola", "ano_letivo", "curso", "serie_ano", "turma", "aluno", "responsavel_aluno", "matricula", "professor", "professor_turma", "diario_frequencia", "avaliacao", "nota", "pre_matricula_inscricao", "educacenso_registro", "portal_educacao_acesso", "educacao_evento" }) sql.Should().Contain("sigov." + table);
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().Contain("entidade_id bigint not null references sigov.entidade(id)");
        sql.Should().Contain("create or replace view sigov.vw_educacao_dashboard");
        sql.Should().NotContain("create schema educacao");
        sql.Should().NotContain("create schema educ");
    }

    [Fact]
    public void Repository_Usa_Dapper_Parametrizado_Tenant_Entidade_Auditoria_Outbox()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Educacao/EducacaoRepository.cs"));
        code.Should().Contain("tenant_id = @TenantId");
        code.Should().Contain("entidade_id = @EntidadeId");
        code.Should().Contain("sigov.educacao_evento");
        code.Should().Contain("cast(@DadosSensiveisJson as jsonb)");
        code.Should().Contain("vagas_ocupadas = vagas_ocupadas + 1");
    }

    [Fact]
    public void Api_E_Web_Exposicoes_Estruturais_Existem()
    {
        File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/EducacaoControllers.cs")).Should().Contain("api/educacao/escolas").And.Contain("api/educacao/dashboard").And.Contain("api/educacao/export");
        File.Exists(Path.Combine(Root, "src/Sigov.Web/Views/Educacao/Dashboard.cshtml")).Should().BeTrue();
        File.Exists(Path.Combine(Root, "src/Sigov.Web/wwwroot/js/modules/educacao.dashboard.js")).Should().BeTrue();
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "sigov.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
