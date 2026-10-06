using Sigov.Testing;
using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class SaasComercialTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260608120000_saas_comercial_white_label_planos.sql"));
    [Fact] public void Migration_cria_tabelas_no_schema_sigov_sem_schema_saas() { Migration.Should().Contain("sigov.saas_plano"); Migration.Should().Contain("sigov.saas_tenant_branding"); Migration.Should().NotContain("create schema " + "saas"); }
    [Fact] public void Migration_contem_fluxo_comercial_completo() { Migration.Should().Contain("sigov.saas_solicitacao_cliente"); Migration.Should().Contain("sigov.saas_assinatura"); Migration.Should().Contain("sigov.saas_onboarding_cliente"); Migration.Should().Contain("sigov.saas_perfil_template"); }
    [Fact] public void Migration_contem_isolamento_tenant_operacional() { Migration.Should().Contain("tenant_id bigint not null references sigov.tenant(id)"); Migration.Should().Contain("unique(tenant_id"); }

    [Fact] public void Manifest_registra_drift_rc50_29_antes_da_ordem_publicada()
    {
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        manifest.Should().Contain("\"version\": \"20260813193000\"");
        manifest.Should().Contain("20260813193000_rc50_29_parametros_modulos.sql");
        manifest.Should().Contain("755a77f67431091f9e83a86703816e5d8f50c20c5eae6091fd1e3fe765fe6f2d");
        manifest.IndexOf("\"version\": \"20260813193000\"", StringComparison.Ordinal).Should().BeGreaterThan(0);
        manifest.IndexOf("\"version\": \"20260813193000\"", StringComparison.Ordinal).Should().BeLessThan(manifest.IndexOf("\"version\": \"20260813223000\"", StringComparison.Ordinal));
    }

    [Fact] public void Manifest_tem_211_migrations_registradas_para_a_rc_saas_aut()
    {
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        manifest.Split("\"version\": \"", StringSplitOptions.None).Length.Should().Be(212); // 211 entradas + texto inicial
    }

    [Fact] public void Migration_comercial_familia_B_idempotente_e_politica_de_downgrade_parametrizada()
    {
        var b = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261005110000_rcsaas_aut_03_comercial_planos_limites.sql"));
        b.Should().Contain("add column if not exists valor_quantidade");
        b.Should().Contain("add column if not exists unidade_quantidade");
        b.Should().Contain("DOWNGRADE_BLOQUEIO_NOVAS_ALOCACOES");
        b.Should().Contain("where not exists");
        b.Should().Contain("valor_quantidade = 10");
    }

    [Fact] public void Migration_corretiva_cria_chave_dashboard_administrar_espelhando_grants_da_plataforma()
    {
        var m = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261005120000_rcsaas_aut_04_dashboard_administrar.sql"));
        m.Should().Contain("'saas.superadmin.dashboard.administrar'");
        m.Should().Contain("where not exists");
        m.Should().Contain("p.chave = 'saas.plataforma.administrar'");
        m.Should().Contain("upper(pp.efeito) = 'PERMITIR'");
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        manifest.Should().Contain("\"version\": \"20261005120000\"");
        manifest.Should().Contain("20261005120000_rcsaas_aut_04_dashboard_administrar.sql");
    }

    [Fact] public void Migration_06v2_normaliza_tuplos_canonicos_superadmin_e_plataforma()
    {
        var m = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261006230000_rcsaas_aut_06_normalizacao_superadmin.sql"));
        m.Should().Contain("recurso = 'plataforma'");
        m.Should().Contain("chave = 'saas.plataforma.administrar'");
        m.Should().Contain("recurso = 'saas.superadmin.dashboard'");
        m.Should().Contain("recurso = 'saas.superadmin.autorizacao'");
        m.Should().Contain("raise exception");
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        manifest.Should().Contain("\"version\": \"20261006230000\"");
        manifest.Should().Contain("20261006230000_rcsaas_aut_06_normalizacao_superadmin.sql");
        manifest.Should().Contain("0d091e37144ec119c5d7c59c7fe1fa4e11aaf6d58d56cdd7cc85204f6c154987");
    }

    [Fact] public void Concorrencia_upgrade_lock_serializa_em_sentenca_single_table_sem_join()
    {
        // Regressao do GATE.G (RC-SAAS-AUT Etapa B): no PG 16 / READ COMMITTED, senteca FOR UPDATE
        // com JOIN sofre recheck EvalPlanQual que pode descartar a linha apos commit concorrente
        // (dois upgrades simultaneos falhavam). Fix validado ao vivo: lock em sentenca single-table
        // em saas_assinatura + escolha da melhor linha (ATIVA primeiro, depois mais recente) em memoria.
        var src = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/Comercial/SaasAssinaturaComercialService.cs"));
        var start = src.IndexOf("select a.id as Id", StringComparison.Ordinal);
        var end = src.IndexOf("for update of a;", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "sentenca de lock presente");
        end.Should().BeGreaterThan(start, "clausula for update presente");
        var lockSql = src.Substring(start, end - start + "for update of a;".Length);
        var lockSqlLower = lockSql.ToLowerInvariant();
        lockSqlLower.Should().Contain("from sigov.saas_assinatura a");
        lockSqlLower.Should().NotContain("join");
        lockSqlLower.Should().NotContain("order by");
        lockSqlLower.Should().NotContain("limit");
        src.Should().Contain(".OrderBy(l => l.Status == \"ATIVA\" ? 0 : 1)");
        src.Should().Contain(".ThenByDescending(l => l.CriadoEm)");
    }
}
