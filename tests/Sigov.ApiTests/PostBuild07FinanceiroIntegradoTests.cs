using FluentAssertions;
using Sigov.Testing;
using Xunit;

namespace Sigov.ApiTests;

public sealed class PostBuild07FinanceiroIntegradoTests
{
    private static readonly string Migration = File.ReadAllText(TestRepoPath.Get("database/postgres/migrations/20260610180000_pos_build_07_financeiro_integrado.sql"));
    private static readonly string Api = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/FinanceiroControllers.cs")) + File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/FinanceiroComercialController.cs"));
    private static readonly string Sidebar = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Shared/_Sidebar.cshtml"));

    [Fact]
    public void Migration_cria_tabelas_financeiras_com_tenant_id_e_idempotencia()
    {
        foreach (var table in new[] { "financeiro_plano_conta", "financeiro_centro_custo", "financeiro_natureza", "financeiro_conta_bancaria", "financeiro_forma_pagamento", "financeiro_conta_receber", "financeiro_conta_pagar", "financeiro_movimento", "financeiro_baixa_receber", "financeiro_baixa_pagar", "financeiro_conciliacao", "financeiro_rateio", "financeiro_fluxo_caixa_snapshot", "financeiro_configuracao" })
        {
            Migration.Should().Contain($"create table if not exists sigov.{table}");
        }

        Migration.Should().Contain("tenant_id bigint not null").And.Contain("create index if not exists").And.Contain("on conflict");
    }

    [Fact]
    public void Catalogo_e_permissoes_financeiras_estao_seedados()
    {
        var catalog = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Commercial/ModuleCatalogService.cs"));
        catalog.Should().Contain("financeiro_empresarial").And.Contain("financeiro_publico").And.Contain("BUSINESS_FINANCE").And.Contain("GOV_PLUS");
        Migration.Should().Contain("financeiro.contas_receber.baixar").And.Contain("financeiro.contas_pagar.estornar").And.Contain("financeiro.conciliacao.concluir");
    }

    [Fact]
    public void Apis_financeiras_empresariais_exigem_modulo_e_expoem_fluxos_principais()
    {
        Api.Should().Contain("RequireModule(\"financeiro_empresarial\")");
        foreach (var route in new[] { "api/financeiro/centros-custo", "api/financeiro/contas-bancarias", "api/financeiro/formas-pagamento", "api/financeiro/contas-receber", "api/financeiro/contas-pagar", "api/financeiro/movimentos", "api/financeiro/fluxo-caixa", "api/financeiro/conciliacoes" })
        {
            Api.Should().Contain(route);
        }

        Api.Should().Contain("ILogger").And.Contain("try").And.Contain("catch").And.Contain("correlationId");
    }

    [Fact]
    public void Telas_menu_docs_e_demo_foram_entregues()
    {
        Sidebar.Should().Contain("/Financeiro/Dashboard")
            .And.Contain("/Empresarial/Estoque/Compras")
            .And.Contain("/Industria/Custos");
        File.Exists(TestRepoPath.Get("src/Sigov.Web/Views/Financeiro/FinanceiroEmpresarial.cshtml")).Should().BeTrue();
        File.ReadAllText(TestRepoPath.Get("scripts/demo-local.ps1")).Should().Contain("http://localhost:8080/Financeiro/Conciliacao");
        File.Exists(TestRepoPath.Get("docs/financeiro-integrado.md")).Should().BeTrue();
        File.Exists(TestRepoPath.Get("docs/conciliacao-bancaria.md")).Should().BeTrue();
    }

    private static readonly string Repositorio = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Financeiro/FinanceiroRepositories.cs"));
    private static readonly string Contratos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Financeiro/FinanceiroContracts.cs"));
    private static readonly string Servicos = File.ReadAllText(TestRepoPath.Get("src/Sigov.Application/Financeiro/FinanceiroServices.cs"));
    private static readonly string CoreJs = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/financeiro.core.js"));

    [Fact]
    public void Idempotencia_escopos_header_e_tela_estao_ligados()
    {
        // Reserva transacional protegida pelo unique index (mesma chave + carga não duplica).
        Repositorio.Should().Contain("insert into sigov.financeiro_idempotencia");
        Repositorio.Should().Contain("on conflict (tenant_id, escopo, chave) do nothing");

        foreach (var escopo in new[] { "empenho.criar", "empenho.anular", "liquidacao.criar", "liquidacao.anular", "pagamento.criar", "pagamento.cancelar", "receita.lancamento.criar", "receita.arrecadacao.criar", "receita.lancamento.cancelar", "receita.arrecadacao.cancelar", "conferencia.ajustar" })
        {
            Repositorio.Should().Contain($"ConsultarReservarAsync(cn,nx,t,\"{escopo}\",chave");
            Repositorio.Should().Contain($"ConfirmarDocumentoAsync(cn,nx,t,\"{escopo}\",chave");
        }

        // Tipos de replay/conflito nos contratos e pulso de auditoria condicionado ao replay.
        Contratos.Should().Contain("FinanceiroIdempotenciaConflitoException").And.Contain("record struct FinanceiroResultadoComando(long DocumentoId, bool Replay)");
        Servicos.Should().Contain("if (!op.Replay)").And.Contain("if (!replay)").And.Contain("catch (FinanceiroIdempotenciaConflitoException ex)");

        // Controller lê o header HTTP e as telas reenviam a mesma chave em nova tentativa.
        Api.Should().Contain("[\"Idempotency-Key\"]").And.Contain("IdempotencyKey()");
        CoreJs.Should().Contain("'Idempotency-Key'").And.Contain("crypto.randomUUID").And.Contain("chaveAcao").And.Contain("concluirAcao");
    }

    private static readonly string ApiJs = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/sigov.api.js"));
    private static readonly string ConferenciaJs = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/wwwroot/js/modules/financeiro.conferencia.js"));
    private static readonly string ConferenciaView = File.ReadAllText(TestRepoPath.Get("src/Sigov.Web/Views/Financeiro/Conferencia.cshtml"));

    [Fact]
    public void Conferencia_ajuste_e_transacional_com_dicionario_fechado_lock_evento_e_409()
    {
        // Dicionário fechado tipo -> (tabela, coluna, recálculo) com os 7 tipos válidos.
        Repositorio.Should().Contain("AlvosConferencia").And.Contain("TiposConferencia");
        foreach (var tipo in new[] { "EMPENHO", "LIQUIDACAO", "PAGAMENTO", "LANCAMENTO", "ARRECADACAO", "LIQUIDACAO_EMPENHO", "PAGAMENTO_EMPENHO" })
        {
            Repositorio.Should().Contain(tipo);
        }

        // Lock da linha alvo (parent-first) dentro da transação + recálculo + conflito explícito + deadlock sem retry cego.
        Repositorio.Should().Contain("for update")
            .And.Contain("409: A diferença do documento mudou desde a prévia")
            .And.Contain("update sigov.{alvo.Tabela} set {alvo.Coluna}={alvo.Coluna}-@Delta")
            .And.Contain("catch (NpgsqlException npe) when(npe.SqlState is \"40001\" or \"40P01\")");

        // Evento AJUSTE_CONFERENCIA na mesma transação com antes/depois, autor, justificativa e correlationId.
        Repositorio.Should().Contain("insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,correlation_id,created_by)")
            .And.Contain("tipo=\"AJUSTE_CONFERENCIA\"")
            .And.Contain("antes,depois,diferenca=deltaAtual,justificativa,autor=u,correlationId");

        // Resultado tipado e mensagens explícitas (nunca simula sucesso).
        Contratos.Should().Contain("enum ConferenciaAjusteResultado { Aplicado = 0, Replay = 1, SemAlteracao = 2 }");
        Servicos.Should().Contain("if (resultado != ConferenciaAjusteResultado.Replay)")
            .And.Contain("\"AJUSTE_CONFERENCIA\"")
            .And.Contain("Documento não localizado na conferência. Recarregue a tela e tente novamente.")
            .And.Contain("Nenhuma alteração necessária: os valores do documento já estão consistentes.");

        // Controller expõe o retorno tipado (enum serializado como string no JSON).
        Api.Should().Contain("ActionResult<ApiResponse<ConferenciaAjusteResultado>>");
    }

    [Fact]
    public void Conferencia_expoe_totais_globais_do_escopo_sem_perder_filtros()
    {
        // Totais sobre TODO o escopo filtrado antes da paginação + instante da consulta.
        Contratos.Should().Contain("public sealed record ConferenciaResponse(IReadOnlyCollection<ConferenciaItemResponse> Itens, long TotalRegistros, long TotalDivergencias, decimal SomaAbsDiferencas, int Page, int PageSize, DateTimeOffset Instante)");

        // CTE única reutilizada (itens + contagens na mesma consulta estável) e validação fechada de tipo.
        var usos = Repositorio.Split("SqlCteConferencia", StringSplitOptions.None).Length - 1;
        usos.Should().BeGreaterOrEqualTo(3);
        Repositorio.Should().Contain("Array.IndexOf(TiposConferencia,tipo)<0")
            .And.Contain("Tipo de documento inválido para conferência");
    }

    [Fact]
    public void Lancamento_nao_cancela_com_arrecadacoes_vigentes_e_mensagem_nao_genericizada()
    {
        // Bloqueio explícito no repositório...
        Repositorio.Should().Contain("Não é possível cancelar o lançamento: há {abVigentes} arrecadação(ões) vigente(s). Cancele as arrecadações primeiro.");
        // ...e o serviço repassa a mensagem ao cliente em vez de engolir em "Erro ao cancelar lançamento."
        Servicos.Should().Contain("ex.Message.StartsWith(\"Não é possível\") ? ex.Message : \"Erro ao cancelar lançamento.\");");
    }

    [Fact]
    public void Web_destino_da_api_por_ambiente_kpis_globais_e_ajuda_contextual()
    {
        // A7: destino configurável > HTTPS > fallback localhost com aviso explícito.
        ApiJs.Should().Contain("window.Sigov_API_BASE_URL").And.Contain("window.location.protocol === 'https:'").And.Contain("console.warn").And.Contain("http://localhost:5001");

        // Bloco B: KPIs globais reais do escopo filtrado e pager real no JS da conferência.
        ConferenciaJs.Should().Contain("r.somaAbsDiferencas").And.Contain("r.totalDivergencias").And.Contain("r.totalRegistros").And.Contain("r.instante").And.Contain("return true");

        // Bloco B: view com rótulos globais ("escopo filtrado") e ajuda contextual anular x cancelar x estornar.
        ConferenciaView.Should().Contain("escopo filtrado").And.Contain("Anular, cancelar ou estornar?");

        // A8: erros classificados por status, campos escapados e o modal só fecha com true.
        CoreJs.Should().Contain("'Sessão expirada'").And.Contain("'Permissão insuficiente'").And.Contain("if (ok === true) _confirmModal.hide()");
    }
}
