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

    [Fact] public void Manifest_tem_217_migrations_registradas_para_a_rc_saas_aut()
    {
        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        // RC-EVO A: +1 entry (20261006233000_evo_a01_tuplos_e_grants_jornada) → 212 entradas + texto inicial.
        // RC-EVO A02: +1 entry (20261004090000_evo_a02_precond_tenant_vinculo) → 213 entradas + texto inicial.
        // RC-EVO A03: +1 entry (20261006110000_evo_a03_precond_credencial_plataforma) → 214 entradas + texto inicial.
        // RC-EVO-RH/folha: +1 entry (20261007100000_evo_rh_folha_feriado_parametros_grants) → 215 entradas + texto inicial.
        // RC-EVO-RH s8: +1 entry (20261007110000_evo_rh_portal_grants) → 216 entradas + texto inicial.
        // RC-EVO-RH s9: +1 entry (20261007120000_evo_rh_folha_financeira_parametros) → 217 entradas + texto inicial.
        // RC-EVO-A S3.3: +1 entry (20261007130000_evo_a_s33_limite_modulos_plano) → 218 entradas + texto inicial.
        // RC-EVO-B: +2 entries (20261008120000_evo_rh_ponto_politica_tolerancia_tipo_empenho;
        //           20261008130000_evo_liberacao_cnpj_placeholder_plataforma) → 220 entradas + texto inicial.
        manifest.Split("\"version\": \"", StringSplitOptions.None).Length.Should().Be(221);
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

    [Fact]
    public void RC_EVO_S3_2_limite_comercial_lock_single_table_e_contagem_fresca_admite_exatamente_uma_ultima_vaga()
    {
        // Regressão RC-EVO S3.2: o antigo UsoComLock (UsoBase + " for update of a") era um SELECT de lock
        // com JOIN + ORDER BY/LIMIT — mesmo risco do GATE.G em PG16/READ COMMITTED (o recheck EvalPlanQual
        // pode perder a linha após aguardar o lock concorrente), e a contagem de usuários embutida naquele
        // mesmo SELECT não garantia snapshot fresco do usuário commitado pela disputa serializada.
        // Fix: lock single-table nas assinaturas ATIVAS + escolha da mais recente em memória + contagens
        // independentes executadas com o lock mantido (exatamente uma vencedora na última vaga).
        var src = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/Comercial/SaasLimitValidator.cs"));

        // 1) Sentença de lock single-table: entre SELECT e FOR UPDATE não há JOIN/ORDER BY/LIMIT.
        var start = src.IndexOf("select id as Id, plano_id as PlanoId, created_at as CriadoEm, data_fim as DataFim", StringComparison.Ordinal);
        var end = src.IndexOf("for update", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "sentença de lock transacional presente");
        end.Should().BeGreaterThan(start, "cláusula for update presente");
        var lockSql = src.Substring(start, end - start + "for update".Length).ToLowerInvariant();
        lockSql.Should().Contain("from sigov.saas_assinatura");
        lockSql.Should().Contain("status='ativa'");
        lockSql.Should().NotContain("join");
        lockSql.Should().NotContain("order by");
        lockSql.Should().NotContain("limit");

        // 2) O padrão antigo sumiu: UsoComLock removido e o ORDER BY/LIMIT sobrevive apenas no UsoBase
        //    (caminho não transacional de leitura de resumo, sem lock).
        src.Should().NotContain("UsoComLock");
        var ordPos = src.IndexOf("order by a.created_at desc limit 1", StringComparison.Ordinal);
        ordPos.Should().BeGreaterThan(0, "UsoBase preserva a semântica original de leitura");
        src.IndexOf("order by a.created_at desc limit 1", ordPos + 1, StringComparison.Ordinal)
            .Should().BeLessThan(0, "nenhuma segunda ocorrência de ORDER BY/LIMIT no arquivo");

        // 3) Semântica preservada: mais recente em memória; assinatura ausente ou plano órfão falham explícitos.
        src.Should().Contain(".OrderByDescending(a => a.CriadoEm).FirstOrDefault()");
        src.Should().Contain("if (melhor is null) return UserLimitDecision(tenantId, null);");
        src.Should().Contain("if (plano is null) return UserLimitDecision(tenantId, null);");

        // 4) Contagem de usuários é sentença independente (sem FOR UPDATE/JOIN dentro dela) e o método
        //    transacional executa lock + plano + contagens todos na MESMA transação (lock mantido).
        var cuIdx = src.IndexOf("CountUsuariosAtivos =", StringComparison.Ordinal);
        cuIdx.Should().BeGreaterThan(0);
        var cuLine = src.Substring(cuIdx, src.IndexOf('\n', cuIdx) - cuIdx);
        cuLine.Should().Contain("count(*)::int from sigov.usuario")
            .And.Contain("u.ativo=true")
            .And.NotContain("for update")
            .And.NotContain("join");
        var txStart = src.IndexOf("public async Task<SaasLimitValidationResult> ValidateUserLimitTxAsync", StringComparison.Ordinal);
        var txEnd = src.IndexOf("private static SaasLimitValidationResult UserLimitDecision", txStart, StringComparison.Ordinal);
        txStart.Should().BeGreaterThan(0);
        txEnd.Should().BeGreaterThan(txStart);
        var txBody = src.Substring(txStart, txEnd - txStart);
        txBody.Should().Contain("new CommandDefinition(LockAssinaturasAtivas");
        txBody.Should().Contain("new CommandDefinition(LimitePlanoApósLock");
        txBody.Should().Contain("new CommandDefinition(CountUsuariosAtivos");
        txBody.Should().Contain("new CommandDefinition(CountModulosAtivos");
        txBody.Split(", transaction,", StringSplitOptions.None).Length
            .Should().Be(5, "as 4 sentenças rodam na transação que mantém o lock (3 ocorrências + abertura)");
    }

    [Fact]
    public void RC_EVO_S3_3_limite_modulos_migration_manifest_consolidados_e_gate_transacional()
    {
        // Regressão RC-EVO S3.3: limite comercial de módulos do plano — migration aditiva idempotente
        // (saas_plano.limite_modulos, null = ilimitado), validação transacional que conta contratos vigentes
        // na fonte contratual tenant_modulo_contratado sob o lock single-table do S3.2 e negação com motivo
        // canônico LIMITE_ATINGIDO (403 padronizado na camada Web).
        var mig = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20261007130000_evo_a_s33_limite_modulos_plano.sql"));
        mig.Should().Contain("alter table sigov.saas_plano add column if not exists limite_modulos int");
        mig.Should().Contain("drop constraint if exists ck_saas_plano_limites");
        mig.Should().Contain("limite_modulos is null or limite_modulos >= 0");
        mig.Should().Contain("raise exception");
        mig.ToLowerInvariant().Should().NotContain("drop table").And.NotContain("truncate");

        var manifest = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/manifest.json"));
        manifest.Should().Contain("\"version\": \"20261007130000\"");
        manifest.Should().Contain("20261007130000_evo_a_s33_limite_modulos_plano.sql");
        manifest.Should().Contain("b98f2b5cbcbc2cc272efdc072498ee3e8cbefcbd086456e388bdf9fcd126e998");

        // Sincronização dos 6 consolidados (regra 7), incluindo o backfill de 20261007120000 no par postgres.
        foreach (var script in new[]
        {
            "script_completo.sql", "script_completop.sql", "script_completo_dev.sql",
            "database/script_completo.sql", "database/postgres/script_completo.sql", "database/postgres/script_completo_dev.sql"
        })
        {
            var consolidado = File.ReadAllText(TestRepoPath.Get(script));
            consolidado.Should().Contain("MIGRATION: 20261007130000_evo_a_s33_limite_modulos_plano.sql");
            consolidado.Should().Contain("limite_modulos is null or limite_modulos >= 0");
            consolidado.Split("MIGRATION: 20261007130000", StringSplitOptions.None).Length
                .Should().Be(2, $"{script}: bloco 130000 exatamente uma vez");
            consolidado.Split("MIGRATION: 20261007120000", StringSplitOptions.None).Length
                .Should().Be(2, $"{script}: bloco 120000 presente (backfill no par postgres)");
        }

        var src = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/Comercial/SaasLimitValidator.cs"));
        src.Should().Contain("ValidateModuleLimitTxAsync");
        var ccIdx = src.IndexOf("CountContratosVigentes =", StringComparison.Ordinal);
        ccIdx.Should().BeGreaterThan(0, "contagem de contratos vigentes presente");
        var ccLine = src.Substring(ccIdx, src.IndexOf('\n', ccIdx) - ccIdx);
        ccLine.Should().Contain("tenant_modulo_contratado")
            .And.Contain("'CONTRATADO','HABILITADO','ATIVO','TRIAL','EM_IMPLANTACAO','BETA'")
            .And.Contain("vigencia_fim>=current_date")
            .And.NotContain("for update")
            .And.NotContain("join");

        var txStart = src.IndexOf("public async Task<SaasLimitValidationResult> ValidateModuleLimitTxAsync", StringComparison.Ordinal);
        var txEnd = src.IndexOf("private static SaasLimitValidationResult ModuleLimitDecision", txStart, StringComparison.Ordinal);
        txStart.Should().BeGreaterThan(0);
        txEnd.Should().BeGreaterThan(txStart);
        var txBody = src.Substring(txStart, txEnd - txStart);
        txBody.Should().Contain("new CommandDefinition(LockAssinaturasAtivas");
        txBody.Should().Contain("new CommandDefinition(LimitePlanoApósLock");
        txBody.Should().Contain("new CommandDefinition(CountContratosVigentes");
        txBody.Split(", transaction,", StringSplitOptions.None).Length
            .Should().Be(4, "as 3 sentenças rodam na transação que mantém o lock (3 ocorrências + abertura)");
        src.Should().Contain("if (melhor is null) return ModuleLimitDecision(tenantId, null);");
        src.Should().Contain("if (plano is null) return ModuleLimitDecision(tenantId, null);");
        src.Should().Contain("row.LimiteModulos is null || row.ContratosVigentes < row.LimiteModulos");

        var svc = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Saas/SaasTenantAdministrationService.cs"));
        var mutStart = svc.IndexOf("private async Task<SaasModuleContractResult> MutateAsync", StringComparison.Ordinal);
        mutStart.Should().BeGreaterThan(0);
        var mutEnd = svc.IndexOf("private static SaasModuleContractResult Rollback", mutStart, StringComparison.Ordinal);
        mutEnd.Should().BeGreaterThan(mutStart);
        var mutBody = svc.Substring(mutStart, mutEnd - mutStart);
        mutBody.Should().Contain("limitValidator.ValidateModuleLimitTxAsync(connection, transaction, command.TenantId, cancellationToken)");
        mutBody.Should().Contain("Rollback(transaction, moduloLimit.Alert ?? \"Limite de módulos do plano atingido.\", SaasForbiddenMotivo.LimiteAtingido)");

        var ctl = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Controllers/SaasAdminController.cs"));
        ctl.Split("SAAS_MODULO_CONTRATAR_LIMITE", StringSplitOptions.None).Length
            .Should().Be(2, "auditoria SAAS_MODULO_CONTRATAR_LIMITE registrada uma única vez");
        ctl.Split("SAAS_MODULO_REATIVAR_LIMITE", StringSplitOptions.None).Length
            .Should().Be(2, "auditoria SAAS_MODULO_REATIVAR_LIMITE registrada uma única vez");
        ctl.Should().Contain("ForbiddenResponse.Registrar(this, result.Motivo403.Value, result.Message)");
    }
}
