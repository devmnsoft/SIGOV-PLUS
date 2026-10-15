using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Health;
using Sigov.Application.Orcamento;
using Sigov.Application.Rh.Previdencia;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Orcamento;

// RC-EVO Ciclo 2 Sprint 13 (BR-13.1 a BR-13.8): motor de orcamento e responsabilidade fiscal
// multi-esfera. Banco = autoridade (regras 11/12): estrutura Lei 4.320/64 (funcao/subfuncao
// como catalogos nacionais globais), disponibilidade = dotacao inicial + creditos adicionais -
// reducoes - empenhado (SEM rollover entre exercicios), limites LRF por esfera com override por
// tenant (LC 101/2000 art. 20/19; MDE CF art. 212 parametrico por esfera), previsao bimestral
// (art. 51) e RREO (art. 54) gerados SOMENTE dos dados reais do exercicio. Nenhum percentual,
// valor ou prazo existe fora de tabelas/parametros (regras 22/24). Falhas nomeadas explicitas
// (regra 13); 1 ato = 1 transacao com locks FOR UPDATE (padrao S12).
public sealed class OrcamentoLrfRepository(NpgsqlConnectionFactory factory, IDatabaseObjectInspector inspector, IDateTimeProvider clock) : IOrcamentoService
{
    static readonly string[] Tabelas = {
        "orcamento_funcao","orcamento_subfuncao","orcamento_programa","orcamento_acao",
        "orcamento_dotacao","orcamento_credito_adicional","orcamento_empenho","orcamento_empenho_item",
        "orcamento_liquidacao","orcamento_liquidacao_item","orcamento_receita_prevista","orcamento_receita_arrecadacao",
        "orcamento_limite_lrf","orcamento_lrf_apuracao","orcamento_previsao_bimestral","orcamento_rreo_lote",
        "orcamento_rreo_item","orcamento_estado_exercicio"
    };

    static readonly string[] TiposAcao = { "PROJETO", "ATIVIDADE", "SUBPROJETO" };
    static readonly string[] Poderes = { "EXECUTIVO", "LEGISLATIVO", "JUDICIARIO" };
    static readonly string[] Categorias = { "CORRENTE", "INVESTIMENTO", "AMORTIZACAO_DIVIDA", "OUTRA_DESPESA" };
    static readonly string[] Grupos = { "PESSOAL_E_ENCARGOS", "JUROS_E_ENCARGOS_DIVIDA", "OUTRAS_DESPESAS_CORRENTES", "INVESTIMENTOS", "AMORTIZACAO_DA_DIVIDA", "OUTRAS_DESPESAS_CAPITAIS" };
    static readonly string[] TiposCredito = { "SUPLEMENTAR", "ESPECIAL", "EXTRAORDINARIO" };
    static readonly string[] TiposEmpenho = { "CP", "CP_GLOBAL", "SUPLEMENTAR" };
    static readonly string[] NaturezasReceita = { "TRIBUTARIA", "TRANSFERENCIAS", "PATRIMONIAL", "SERVICOS", "DIVERSAS_RECEITAS" };

    async Task GuardAsync(CancellationToken ct)
    {
        foreach (var t in Tabelas)
            if (!await inspector.TableExistsAsync("sigov", t, ct).ConfigureAwait(false))
                throw new OrcamentoRecursoNaoDisponivelException(t);
    }

    long U(OrcamentoContext x) => x.UsuarioId ?? throw new InvalidOperationException("usuário obrigatório no contexto.");
    long Ent(OrcamentoContext x) => x.EntidadeId ?? throw new EntidadeSemEsferaException();

    // BeginTransaction nao abre a conexao automaticamente (Dapper so abre na execucao de comandos);
    // Open explicit segue o padrao da codebase (PrevidenciaFolhaRepository, AlmoxarifadoService etc.).
    static NpgsqlTransaction BeginTx(NpgsqlConnection c)
    {
        if (c.State != System.Data.ConnectionState.Open)
            c.Open();
        return c.BeginTransaction();
    }

    static string SqlParametro =>
        """
        select coalesce((select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=c.pid and not val.is_deleted limit 1), c.vp)
        from (select id as pid, valor_padrao::text as vp from sigov.parametro_modulo where modulo='ORCAMENTO' and codigo=@Cod and ativo and not is_deleted limit 1) c
        """;

    async Task<string> ParamTextoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, string codigo, CancellationToken ct)
    {
        var v = await c.ExecuteScalarAsync<string>(new CommandDefinition(SqlParametro, new { T = x.TenantId, Cod = codigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v)) throw new OrcamentoRecursoNaoDisponivelException("parametro_modulo.ORCAMENTO." + codigo);
        return v.Trim().Trim('"');
    }

    async Task<int> ParamIntAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, string codigo, CancellationToken ct)
    {
        var v = await ParamTextoAsync(c, tx, x, codigo, ct).ConfigureAwait(false);
        if (!int.TryParse(v, out var n)) throw new OrcamentoRecursoNaoDisponivelException("parametro_modulo.ORCAMENTO." + codigo + "=" + v);
        return n;
    }

    async Task<bool> ParamBoolAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, string codigo, CancellationToken ct)
    {
        var v = await ParamTextoAsync(c, tx, x, codigo, ct).ConfigureAwait(false);
        if (!bool.TryParse(v, out var b)) throw new OrcamentoRecursoNaoDisponivelException("parametro_modulo.ORCAMENTO." + codigo + "=" + v);
        return b;
    }

    // Regras 21-24: a esfera vem da entidade do contexto institucional; ausente = falha explicita.
    async Task<string> EsferaAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, CancellationToken ct)
    {
        var e = Ent(x);
        var s = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            select esfera_governo from sigov.entidade where id=@E and tenant_id=@T and ativo=true and not is_deleted
            """, new { E = e, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(s)) throw new EntidadeSemEsferaException();
        return s.Trim().ToLowerInvariant();
    }

    static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    async Task<(long Id, DateOnly Inicio, DateOnly Fim)> ResoloverExercicioAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, int ano, CancellationToken ct)
    {
        var r = await c.QueryFirstOrDefaultAsync<(long Id, DateOnly Ini, DateOnly Fim)>(new CommandDefinition(
            "select id, data_inicio, data_fim from sigov.exercicio where entidade_id=@E and ano=@A and not is_deleted",
            new { E = Ent(x), A = ano }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (r.Id == 0) throw new OrcamentoExercicioInexistenteException(ano);
        return (r.Id, r.Ini, r.Fim);
    }

    async Task ExigirExercicioAbertoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, long exId, int ano, CancellationToken ct)
    {
        var s = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select situacao from sigov.orcamento_estado_exercicio where exercicio_id=@X and not is_deleted",
            new { X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (s == "ENCERRADO") throw new OrcamentoExercicioEncerradoException(ano);
    }

    // ----------------------------- APURACAO LRF (BR-13.5) -----------------------------
    sealed record LimiteRow(string Chave, string Nome, string Tipo, decimal Alvo, decimal Alerta, decimal? Bloq, string BaseReceita);
    sealed record MedidaLrf(string Chave, string Nome, decimal? Bloq, decimal Base, decimal Atual, decimal Pct, string Sit);

    static readonly string SqlNumPessoal =
        """
        select coalesce(sum(e.valor_total - e.valor_anulado),0)
        from sigov.orcamento_empenho e
        join sigov.orcamento_empenho_item i on i.empenho_id=e.id and not i.is_deleted
        join sigov.orcamento_dotacao d on d.id=i.dotacao_id and not d.is_deleted
        join sigov.orcamento_acao a on a.id=d.acao_id and not a.is_deleted
        where e.tenant_id=@T and e.exercicio_id=@X and e.situacao='ATIVO' and not e.is_deleted
          and a.grupo_despesa='PESSOAL_E_ENCARGOS' and a.poder=@P
        """;
    static readonly string SqlRaAntevigentes =
        """
        select coalesce(sum(e.valor_total - e.valor_anulado - e.valor_liquidado),0)
        from sigov.orcamento_empenho e
        join sigov.exercicio ex on ex.id=e.exercicio_id
        where e.tenant_id=@T and e.situacao='ATIVO' and not e.is_deleted
          and ex.entidade_id=@E and ex.ano <= @A
        """;
    static readonly string SqlRaAtual =
        """
        select coalesce(sum(e.valor_total - e.valor_anulado - e.valor_liquidado),0)
        from sigov.orcamento_empenho e
        where e.tenant_id=@T and e.exercicio_id=@X and e.situacao='ATIVO' and not e.is_deleted
        """;
    static readonly string SqlFuncaoSocial =
        """
        select coalesce(sum(e.valor_total - e.valor_anulado),0)
        from sigov.orcamento_empenho e
        join sigov.orcamento_empenho_item i on i.empenho_id=e.id and not i.is_deleted
        join sigov.orcamento_dotacao d on d.id=i.dotacao_id and not d.is_deleted
        join sigov.orcamento_acao a on a.id=d.acao_id and not a.is_deleted
        join sigov.orcamento_funcao f on f.codigo=a.funcao_codigo and not f.is_deleted
        where e.tenant_id=@T and e.exercicio_id=@X and e.situacao='ATIVO' and not e.is_deleted
          and f.funcao_social
        """;
    static readonly string SqlMde =
        """
        select coalesce(sum(e.valor_total - e.valor_anulado),0)
        from sigov.orcamento_empenho e
        join sigov.orcamento_empenho_item i on i.empenho_id=e.id and not i.is_deleted
        join sigov.orcamento_dotacao d on d.id=i.dotacao_id and not d.is_deleted
        join sigov.orcamento_acao a on a.id=d.acao_id and not a.is_deleted
        join sigov.orcamento_funcao f on f.codigo=a.funcao_codigo and not f.is_deleted
        where e.tenant_id=@T and e.exercicio_id=@X and e.situacao='ATIVO' and not e.is_deleted
          and f.area_saude_educacao
        """;

    // Override do tenant tem precedencia sobre a linha institucional da mesma esfera x chave.
    async Task<IReadOnlyList<LimiteRow>> LimitesEfetivosDbAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, string esfera, CancellationToken ct)
    {
        var rows = await c.QueryAsync<LimiteRow>(new CommandDefinition(
            """
            select l.chave, l.nome, l.tipo_comparacao as tipo, l.valor_alvo as alvo, l.alerta_pct as alerta, l.bloqueio_pct as bloq, l.base_receita as "baseReceita"
            from sigov.orcamento_limite_lrf l
            where l.esfera_governo=@Esf and l.ativo and not l.is_deleted
              and (l.tenant_id is null or l.tenant_id=@T)
            order by l.tenant_id nulls last, l.chave
            """, new { Esf = esfera, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return rows.GroupBy(r => r.Chave).Select(g => g.First()).ToList();
    }

    async Task<decimal> BaseReceitaAsync(NpgsqlConnection c, NpgsqlTransaction? tx, long tenantId, long exId, string baseTipo, CancellationToken ct)
    {
        var col = baseTipo == "ARRECADADA" ? "valor_arrecadado" : "valor_prevista";
        return await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            $@"select coalesce(sum({col}),0) from sigov.orcamento_receita_prevista
               where tenant_id=@T and exercicio_id=@X and not is_deleted",
            new { T = tenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    async Task<(decimal Base, decimal Atual)> MedirLimiteAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long exId, int ano, LimiteRow l, bool incAnte, CancellationToken ct)
    {
        var base_ = await BaseReceitaAsync(c, tx, x.TenantId, exId, l.BaseReceita, ct).ConfigureAwait(false);
        var chave = l.Chave;
        string sql;
        object pars;
        if (chave.StartsWith("PESSOAL_") && chave.EndsWith("_MAX"))
        {
            var poder = chave.Substring("PESSOAL_".Length, chave.Length - "PESSOAL_".Length - "_MAX".Length);
            sql = SqlNumPessoal;
            pars = new { T = x.TenantId, X = exId, P = poder.ToUpperInvariant() };
        }
        else if (chave == "RESTOS_A_PAGAR_MAX")
        {
            sql = incAnte ? SqlRaAntevigentes : SqlRaAtual;
            pars = incAnte ? new { T = x.TenantId, E = Ent(x), A = ano } : new { T = x.TenantId, X = exId };
        }
        else if (chave == "FUNC_SOCIAL_MIN") { sql = SqlFuncaoSocial; pars = new { T = x.TenantId, X = exId }; }
        else if (chave == "MDE_MIN") { sql = SqlMde; pars = new { T = x.TenantId, X = exId }; }
        else { sql = "select 0::numeric"; pars = new { }; }
        var atual = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(sql, pars, tx, cancellationToken: ct)).ConfigureAwait(false);
        return (base_, atual);
    }

    static decimal Pct(decimal base_, decimal atual) => base_ > 0 ? Math.Round(atual * 100m / base_, 4) : 0m;

    // Situacao dirigida por alerta_pct (faixa de alerta) e bloqueio_pct (bloqueio duro, quando
    // preenchido); valor_alvo e o alvo legal exibido. MIN invertido: queda abaixo de alerta/bloqueio.
    static string SituacaoLrf(string tipo, decimal pct, decimal alerta, decimal? bloq)
    {
        if (tipo == "MIN")
        {
            if (pct >= alerta) return "DENTRO";
            if (bloq is { } b && pct <= b) return "BLOQUEIO";
            return "ALERTA";
        }
        if (pct <= alerta) return "DENTRO";
        if (bloq is { } b2 && pct >= b2) return "BLOQUEIO";
        return "ALERTA";
    }

    async Task<List<MedidaLrf>> ApurarTodosAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long exId, int ano, string esfera, bool incAnte, CancellationToken ct)
    {
        var out_ = new List<MedidaLrf>();
        foreach (var l in await LimitesEfetivosDbAsync(c, tx, x, esfera, ct).ConfigureAwait(false))
        {
            var (b, a) = await MedirLimiteAsync(c, tx, x, exId, ano, l, incAnte, ct).ConfigureAwait(false);
            var p = Pct(b, a);
            out_.Add(new MedidaLrf(l.Chave, l.Nome, l.Bloq, R2(b), R2(a), p, SituacaoLrf(l.Tipo, p, l.Alerta, l.Bloq)));
        }
        return out_;
    }

    async Task PersistirApuracoesAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long exId, string esfera, DateOnly periodo, List<MedidaLrf> linhas, CancellationToken ct)
    {
        foreach (var m in linhas)
        {
            var det = $"base={m.Base:0.00}; atual={m.Atual:0.00}; percentual={m.Pct:0.####}";
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.orcamento_lrf_apuracao(tenant_id,entidade_id,exercicio_id,esfera_governo,periodo,limite_chave,base_valor,valor_atual,percentual,situacao,detalhe,versao,criado_por)
                values (@T,@E,@X,@Esf,@P,@Ch,@B,@A,@Pct,@Sit,@Det,(select coalesce(max(v2.versao),0)+1 from sigov.orcamento_lrf_apuracao v2 where v2.tenant_id=@T and v2.entidade_id=@E and v2.exercicio_id=@X and v2.limite_chave=@Ch),@U)
                """, new { T = x.TenantId, E = Ent(x), X = exId, Esf = esfera, P = periodo, Ch = m.Chave, B = m.Base, A = m.Atual, Pct = m.Pct, Sit = m.Sit, Det = det, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
    }

    // Bloqueio LRF (regra 13/12): o empenho/arrecadacao novo so e negado quando TRANSICIONA para
    // BLOQUEIO apos o movimento — o bloqueio nasce dos dados reais, nunca de catalogo ficticio.
    void VerificarTransicaoBloqueio(List<MedidaLrf> antes, List<MedidaLrf> depois)
    {
        foreach (var d in depois)
        {
            if (d.Sit != "BLOQUEIO") continue;
            var prev = antes.FirstOrDefault(m => m.Chave == d.Chave);
            if (prev is null || prev.Sit != "BLOQUEIO")
                throw new OrcamentoLrfLimiteBloqueadoException(d.Chave, d.Nome, d.Pct, d.Bloq ?? d.Pct);
        }
    }

    // ----------------------------- ESTRUTURA (BR-13.1) ---------------------------------

    public async Task<CatalogoFuncionalDto> CatalogoFuncionalAsync(OrcamentoContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var funcoes = await c.QueryAsync<FuncaoDto>(new CommandDefinition(
            "select codigo,nome,funcao_social as \"funcaoSocial\",area_saude_educacao as \"areaSaudeEducacao\",ativo from sigov.orcamento_funcao where ativo and not is_deleted order by ordem",
            new { }, cancellationToken: ct)).ConfigureAwait(false);
        var subfuncoes = await c.QueryAsync<SubfuncaoDto>(new CommandDefinition(
            "select codigo,nome,funcao_codigo as \"funcaoCodigo\",ativo from sigov.orcamento_subfuncao where ativo and not is_deleted order by ordem",
            new { }, cancellationToken: ct)).ConfigureAwait(false);
        return new CatalogoFuncionalDto(funcoes.ToList(), subfuncoes.ToList());
    }

    public async Task<IReadOnlyList<ProgramaDto>> ListarProgramasAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        return (await c.QueryAsync<ProgramaDto>(new CommandDefinition(
            """
            select id,codigo,nome,objetivo,situacao from sigov.orcamento_programa
            where tenant_id=@T and exercicio_id=@X and not is_deleted order by codigo
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
    }

    public async Task<ProgramaDto> CriarProgramaAsync(OrcamentoContext x, int ano, ProgramaCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome) || string.IsNullOrWhiteSpace(r.Objetivo))
            throw new ArgumentException("codigo, nome e objetivo sao obrigatorios no programa.");
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_programa(tenant_id,entidade_id,exercicio_id,codigo,nome,objetivo,situacao,created_by)
                values (@T,@E,@X,@C,@N,@O,'ATIVO',@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, C = r.Codigo.Trim(), N = r.Nome.Trim(), O = r.Objetivo.Trim(), U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return new ProgramaDto(id, r.Codigo.Trim(), r.Nome.Trim(), r.Objetivo.Trim(), "ATIVO");
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            throw new OrcamentoProgramaJaExisteException(r.Codigo.Trim());
        }
    }

    public async Task<IReadOnlyList<AcaoDto>> ListarAcoesAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        return (await c.QueryAsync<AcaoDto>(new CommandDefinition(
            """
            select id,codigo,nome,tipo,programa_id as "programaId",subfuncao_codigo as "subfuncaoCodigo",funcao_codigo as "funcaoCodigo",poder,categoria_economica as "categoriaEconomica",grupo_despesa as "grupoDespesa",ativo
            from sigov.orcamento_acao where tenant_id=@T and exercicio_id=@X and not is_deleted order by codigo
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
    }

    public async Task<AcaoDto> CriarAcaoAsync(OrcamentoContext x, int ano, AcaoCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome))
            throw new ArgumentException("codigo e nome sao obrigatorios na acao.");
        if (!TiposAcao.Contains(r.Tipo, StringComparer.Ordinal)) throw new OrcamentoAcaoTipoInvalidoException(r.Tipo ?? "");
        if (!Poderes.Contains(r.Poder, StringComparer.Ordinal)) throw new OrcamentoPoderInvalidoException(r.Poder ?? "");
        if (!Categorias.Contains(r.CategoriaEconomica, StringComparer.Ordinal)) throw new OrcamentoCategoriaEconomicaInvalidaException(r.CategoriaEconomica ?? "");
        if (!Grupos.Contains(r.GrupoDespesa, StringComparer.Ordinal)) throw new OrcamentoGrupoDespesaInvalidoException(r.GrupoDespesa ?? "");
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var sf = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(1) from sigov.orcamento_subfuncao where codigo=@C and ativo and not is_deleted",
                new { C = r.SubfuncaoCodigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (sf == 0) throw new OrcamentoSubfuncaoInexistenteException(r.SubfuncaoCodigo ?? "");
            var fn = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(1) from sigov.orcamento_funcao where codigo=@C and ativo and not is_deleted",
                new { C = r.FuncaoCodigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (fn == 0) throw new OrcamentoFuncaoInexistenteException(r.FuncaoCodigo ?? "");
            if (r.ProgramaId is { } pid)
            {
                var pr = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                    "select count(1) from sigov.orcamento_programa where id=@I and tenant_id=@T and exercicio_id=@X and not is_deleted",
                    new { I = pid, T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (pr == 0) throw new OrcamentoProgramaInexistenteException();
            }
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_acao(tenant_id,entidade_id,exercicio_id,programa_id,codigo,nome,tipo,subfuncao_codigo,funcao_codigo,poder,categoria_economica,grupo_despesa,ativo,created_by)
                values (@T,@E,@X,@P,@C,@N,@Tp,@Sf,@Fn,@Po,@Ce,@Gd,true,@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, P = r.ProgramaId, C = r.Codigo.Trim(), N = r.Nome.Trim(), Tp = r.Tipo, Sf = r.SubfuncaoCodigo, Fn = r.FuncaoCodigo, Po = r.Poder, Ce = r.CategoriaEconomica, Gd = r.GrupoDespesa, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return new AcaoDto(id, r.Codigo.Trim(), r.Nome.Trim(), r.Tipo, r.ProgramaId, r.SubfuncaoCodigo, r.FuncaoCodigo, r.Poder, r.CategoriaEconomica, r.GrupoDespesa, true);
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            throw new OrcamentoAcaoJaExisteException(r.Codigo.Trim());
        }
    }

    // ----------------------------- DOTACAO E CREDITOS (BR-13.2) -------------------------

    const string SqlDotacaoSelect =
        """
        select d.id, d.acao_id as "acaoId", a.codigo as "acaoCodigo", a.nome as "acaoNome", a.subfuncao_codigo as "subfuncaoCodigo", a.funcao_codigo as "funcaoCodigo", a.poder,
               d.fonte_recurso_codigo as "fonteRecursoCodigo", d.dotacao_inicial as "dotacaoInicial", d.creditos_suplementares as "creditosSuplementares", d.creditos_especiais as "creditosEspeciais",
               d.creditos_extraordinarios as "creditosExtraordinarios", d.reducoes, d.empenhado, d.liquidado, d.pago,
               (d.dotacao_inicial + d.creditos_suplementares + d.creditos_especiais + d.creditos_extraordinarios - d.reducoes - d.empenhado) as disponivel
        from sigov.orcamento_dotacao d join sigov.orcamento_acao a on a.id=d.acao_id
        """;

    sealed record DotacaoRow(long Id, long AcaoId, string AcaoCodigo, string AcaoNome, string SubfuncaoCodigo, string FuncaoCodigo, string Poder, string FonteRecursoCodigo, decimal DotacaoInicial, decimal CreditosSuplementares, decimal CreditosEspeciais, decimal CreditosExtraordinarios, decimal Reducoes, decimal Empenhado, decimal Liquidado, decimal Pago, decimal Disponivel);

    public async Task<IReadOnlyList<DotacaoDto>> ListarDotacoesAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var rows = await c.QueryAsync<DotacaoRow>(new CommandDefinition(
            SqlDotacaoSelect + " where d.tenant_id=@T and d.exercicio_id=@X and not d.is_deleted and not a.is_deleted order by d.id",
            new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(d => new DotacaoDto(d.Id, d.AcaoId, d.AcaoCodigo, d.AcaoNome, d.SubfuncaoCodigo, d.FuncaoCodigo,
            d.Poder, d.FonteRecursoCodigo, d.DotacaoInicial, d.CreditosSuplementares, d.CreditosEspeciais, d.CreditosExtraordinarios,
            d.Reducoes, R2(d.DotacaoInicial + d.CreditosSuplementares + d.CreditosEspeciais + d.CreditosExtraordinarios - d.Reducoes),
            d.Empenhado, d.Liquidado, d.Pago, d.Disponivel)).ToList();
    }

    public async Task<DotacaoDto> CriarDotacaoAsync(OrcamentoContext x, int ano, DotacaoCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.FonteRecursoCodigo)) throw new ArgumentException("fonte_recurso_codigo e obrigatoria na dotacao.");
        if (r.DotacaoInicial < 0) throw new ArgumentException($"dotacao inicial {r.DotacaoInicial:0.00} nao pode ser negativa.");
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var acao = await c.QueryFirstOrDefaultAsync<(long Id, string Codigo)>(new CommandDefinition(
                "select id,codigo from sigov.orcamento_acao where id=@I and tenant_id=@T and exercicio_id=@X and not is_deleted",
                new { I = r.AcaoId, T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (acao.Id == 0) throw new OrcamentoAcaoInexistenteException();
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_dotacao(tenant_id,entidade_id,exercicio_id,acao_id,fonte_recurso_codigo,dotacao_inicial,created_by)
                values (@T,@E,@X,@A,@F,@V,@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, A = r.AcaoId, F = r.FonteRecursoCodigo.Trim(), V = R2(r.DotacaoInicial), U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            var row = await c.QueryFirstOrDefaultAsync<DotacaoRow>(new CommandDefinition(SqlDotacaoSelect + " where d.id=@I and d.tenant_id=@T", new { I = id, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
            if (row == null) throw new OrcamentoDotacaoInexistenteException();
            return new DotacaoDto(row.Id, row.AcaoId, row.AcaoCodigo, row.AcaoNome, row.SubfuncaoCodigo, row.FuncaoCodigo,
                row.Poder, row.FonteRecursoCodigo, row.DotacaoInicial, row.CreditosSuplementares, row.CreditosEspeciais, row.CreditosExtraordinarios,
                row.Reducoes, R2(row.DotacaoInicial + row.CreditosSuplementares + row.CreditosEspeciais + row.CreditosExtraordinarios - row.Reducoes),
                row.Empenhado, row.Liquidado, row.Pago, row.Disponivel);
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            tx.Rollback();
            var acao = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "select codigo from sigov.orcamento_acao where id=@I and tenant_id=@T", new { I = r.AcaoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
            throw new OrcamentoDotacaoJaExisteException(acao ?? "?", r.FonteRecursoCodigo.Trim());
        }
    }

    public async Task<CreditoAdicionalDto> RegistrarCreditoAsync(OrcamentoContext x, int ano, CreditoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (!TiposCredito.Contains(r.Tipo, StringComparer.Ordinal)) throw new OrcamentoCreditoTipoInvalidoException(r.Tipo ?? "");
        if (r.Valor <= 0) throw new ArgumentException($"credito {r.Valor:0.00} deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(r.AtoLegal)) throw new ArgumentException("ato_legal e obrigatorio no credito adicional.");
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var minJ = await ParamIntAsync(c, tx, x, "ORCAMENTO_CREDITO_JUSTIFICATIVA_MIN_CARACTERES", ct).ConfigureAwait(false);
            if (minJ > 0 && (r.Justificativa?.Trim().Length ?? 0) < minJ)
                throw new OrcamentoJustificativaInsuficienteException("credito adicional", r.Justificativa?.Trim().Length ?? 0, minJ);
            var rep = await c.QueryFirstOrDefaultAsync<(long Id, long DotacaoId, string Tipo, decimal Valor, string Ato, string Just, string Sit, DateTime Criado)>(new CommandDefinition(
                "select id,dotacao_id,tipo,valor,ato_legal,justificativa,situacao,created_at from sigov.orcamento_credito_adicional where tenant_id=@T and chave_idempotencia=@K and not is_deleted",
                new { T = x.TenantId, K = r.ChaveIdempotencia }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (rep.Id != 0) return new CreditoAdicionalDto(rep.Id, rep.DotacaoId, rep.Tipo, rep.Valor, rep.Ato, rep.Just, rep.Sit, rep.Criado, true);
            var dot = await c.QueryFirstOrDefaultAsync<long>(new CommandDefinition(
                "select id from sigov.orcamento_dotacao where id=@I and tenant_id=@T and exercicio_id=@X and not is_deleted for update",
                new { I = r.DotacaoId, T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (dot == 0) throw new OrcamentoDotacaoInexistenteException();
            var col = r.Tipo switch { "SUPLEMENTAR" => "creditos_suplementares", "ESPECIAL" => "creditos_especiais", _ => "creditos_extraordinarios" };
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_credito_adicional(tenant_id,entidade_id,exercicio_id,dotacao_id,tipo,valor,ato_legal,justificativa,chave_idempotencia,aprovado_por,situacao,created_by)
                values (@T,@E,@X,@D,@Tp,@V,@Ato,@J,@K,@U,'APLICADO',@U2) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, D = r.DotacaoId, Tp = r.Tipo, V = R2(r.Valor), Ato = r.AtoLegal.Trim(), J = r.Justificativa?.Trim() ?? "", K = r.ChaveIdempotencia, U = U(x), U2 = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                $"update sigov.orcamento_dotacao set {col}={col}+@V, updated_at=now() where id=@I and tenant_id=@T",
                new { V = R2(r.Valor), I = r.DotacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            var row = await c.QueryFirstAsync<(long Id, string Tipo, decimal Valor, string Ato, string Just, string Sit, DateTime Criado)>(new CommandDefinition(
                "select id,tipo,valor,ato_legal,justificativa,situacao,created_at from sigov.orcamento_credito_adicional where id=@I and tenant_id=@T",
                new { I = id, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
            return new CreditoAdicionalDto(row.Id, r.DotacaoId, row.Tipo, row.Valor, row.Ato, row.Just, row.Sit, row.Criado, false);
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            var rep = await c.QueryFirstAsync<(long Id, long DotacaoId, string Tipo, decimal Valor, string Ato, string Just, string Sit, DateTime Criado)>(new CommandDefinition(
                "select id,dotacao_id,tipo,valor,ato_legal,justificativa,situacao,created_at from sigov.orcamento_credito_adicional where tenant_id=@T and chave_idempotencia=@K",
                new { T = x.TenantId, K = r.ChaveIdempotencia }, cancellationToken: ct)).ConfigureAwait(false);
            return new CreditoAdicionalDto(rep.Id, rep.DotacaoId, rep.Tipo, rep.Valor, rep.Ato, rep.Just, rep.Sit, rep.Criado, true);
        }
    }

    // ----------------------------- EMPENHO CP (BR-13.3) ---------------------------------

    async Task<(EmpenhoDto Dto, bool Idempotente)> CarregarEmpenhoPorChaveAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, string chave, CancellationToken ct)
    {
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select coalesce(max(id),0) from sigov.orcamento_empenho where tenant_id=@T and chave_idempotencia=@K and not is_deleted",
            new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (id == 0) return (null!, false);
        var dto = await MontarEmpenhoAsync(c, tx, x, id, ct).ConfigureAwait(false);
        return (dto, true);
    }

    async Task<EmpenhoDto> MontarEmpenhoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long id, CancellationToken ct)
    {
        var h = await c.QueryFirstAsync<(long Id, string Numero, DateOnly Data, long? Fornecedor, string? FornecedorNome, string Tipo, string Historico, decimal Total, decimal Anulado, decimal Liquidado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select e.id, e.numero, e.data_empenho, e.fornecedor_pessoa_id, coalesce(p.nome,''), e.tipo_empenho, e.historico,
                   e.valor_total, e.valor_anulado, e.valor_liquidado, e.situacao, e.created_at
            from sigov.orcamento_empenho e left join sigov.pessoa p on p.id=e.fornecedor_pessoa_id
            where e.id=@I and e.tenant_id=@T and not e.is_deleted
            """, new { I = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var itens = await c.QueryAsync<(long Id, long DotacaoId, string AcaoCodigo, string AcaoNome, string Fonte, decimal Valor, decimal Anulado, decimal Liquidado)>(new CommandDefinition(
            """
            select i.id, i.dotacao_id, a.codigo, a.nome, d.fonte_recurso_codigo, i.valor, i.valor_anulado, i.valor_liquidado
            from sigov.orcamento_empenho_item i
            join sigov.orcamento_dotacao d on d.id=i.dotacao_id
            join sigov.orcamento_acao a on a.id=d.acao_id
            where i.empenho_id=@I and not i.is_deleted order by i.id
            """, new { I = id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new EmpenhoDto(h.Id, h.Numero, h.Data, h.Fornecedor, h.FornecedorNome, h.Tipo, h.Historico, h.Total, h.Anulado, h.Liquidado, R2(h.Total - h.Anulado - h.Liquidado), h.Sit, h.Criado, false);
    }

    public async Task<EmpenhoDto> AbrirEmpenhoAsync(OrcamentoContext x, int ano, EmpenhoAbrirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Itens is null || r.Itens.Count == 0) throw new EmpenhoSemItensException();
        foreach (var i in r.Itens) if (i.Valor <= 0) throw new ArgumentException($"item de empenho sobre dotacao {i.DotacaoId} com valor {i.Valor:0.00} deve ser > 0.");
        if (!TiposEmpenho.Contains(r.TipoEmpenho, StringComparer.Ordinal)) throw new OrcamentoEmpenhoTipoInvalidoException(r.TipoEmpenho ?? "");
        if (string.IsNullOrWhiteSpace(r.Numero)) throw new ArgumentException("numero e obrigatorio no empenho.");
        var soma = R2(r.Itens.Sum(i => i.Valor));
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        long empId;
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
            var (rep, wasRep) = await CarregarEmpenhoPorChaveAsync(c, tx, x, r.ChaveIdempotencia, ct).ConfigureAwait(false);
            if (wasRep) return rep with { Idempotente = true };
            var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
            var antes = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
            foreach (var grp in r.Itens.GroupBy(i => i.DotacaoId))
            {
                var dot = await c.QueryFirstOrDefaultAsync<(long Id, string AcaoCodigo, string Fonte, decimal Disp)>(new CommandDefinition(
                    """
                    select d.id, a.codigo, d.fonte_recurso_codigo,
                           d.dotacao_inicial + d.creditos_suplementares + d.creditos_especiais + d.creditos_extraordinarios - d.reducoes - d.empenhado
                    from sigov.orcamento_dotacao d join sigov.orcamento_acao a on a.id=d.acao_id
                    where d.id=@I and d.tenant_id=@T and not d.is_deleted and not a.is_deleted for update
                    """, new { I = grp.Key, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (dot.Id == 0) throw new OrcamentoDotacaoInexistenteException();
                var solicitado = R2(grp.Sum(g => g.Valor));
                if (solicitado > dot.Disp) throw new OrcamentoEmpenheiroInsuficienteException(dot.AcaoCodigo, dot.Fonte, solicitado, dot.Disp);
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_dotacao set empenhado=empenhado+@V, updated_at=now() where id=@I and tenant_id=@T",
                    new { V = solicitado, I = grp.Key, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
            empId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_empenho(tenant_id,entidade_id,exercicio_id,numero,data_empenho,fornecedor_pessoa_id,tipo_empenho,historico,valor_total,chave_idempotencia,situacao,created_by)
                values (@T,@E,@X,@N,@D,@F,@Tp,@H,@VT,@K,'ATIVO',@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, N = r.Numero.Trim(), D = r.DataEmpenho, F = r.FornecedorPessoaId, Tp = r.TipoEmpenho, H = r.Historico, VT = soma, K = r.ChaveIdempotencia, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var i in r.Itens)
                await c.ExecuteAsync(new CommandDefinition(
                    "insert into sigov.orcamento_empenho_item(empenho_id,dotacao_id,valor) values (@E,@D,@V)",
                    new { E = empId, D = i.DotacaoId, V = i.Valor }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var depois = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
            VerificarTransicaoBloqueio(antes, depois);
            var apuroAuto = await ParamBoolAsync(c, tx, x, "ORCAMENTO_APURO_LRF_AUTO", ct).ConfigureAwait(false);
            if (apuroAuto) await PersistirApuracoesAsync(c, tx, x, exId, esfera, r.DataEmpenho, depois, ct).ConfigureAwait(false);
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23505" && e.Message.Contains("ux_orcamento_empenho_chave", StringComparison.Ordinal))
        {
            // corrida de idempotencia: a chave foi inserida entre a checagem e o insert.
            var rep2 = await CarregarEmpenhoPorChaveAsync(c, null, x, r.ChaveIdempotencia, ct).ConfigureAwait(false);
            return rep2.Dto;
        }
        var dto = await MontarEmpenhoAsync(c, null, x, empId, ct).ConfigureAwait(false);
        return dto with { Idempotente = false };
    }

    public async Task<EmpenhoDto> AnularEmpenhoAsync(OrcamentoContext x, int ano, EmpenhoAnularRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var minJ = await ParamIntAsync(c, tx, x, "ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES", ct).ConfigureAwait(false);
            if (minJ > 0 && (r.Justificativa?.Trim().Length ?? 0) < minJ)
                throw new OrcamentoJustificativaInsuficienteException("anulacao de empenho", r.Justificativa?.Trim().Length ?? 0, minJ);
            var emp = await c.QueryFirstOrDefaultAsync<(long Id, decimal Total, decimal Liquidado, string Sit)>(new CommandDefinition(
                "select id, valor_total, valor_liquidado, situacao from sigov.orcamento_empenho where id=@I and tenant_id=@T and not is_deleted for update",
                new { I = r.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (emp.Id == 0) throw new OrcamentoEmpenhoInexistenteException();
            if (emp.Sit == "ANULADO") throw new OrcamentoEmpenhoJaAnuladoException();
            if (emp.Liquidado > 0) throw new OrcamentoEmpenhoComLiquidacaoException(emp.Liquidado);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_empenho set situacao='ANULADO', valor_anulado=valor_total, anulado_em=now(), anulado_por=@U, anulacao_justificativa=@J, updated_at=now() where id=@I and tenant_id=@T",
                new { U = U(x), J = r.Justificativa?.Trim() ?? "", I = r.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var itens = await c.QueryAsync<(long DotacaoId, decimal Valor)>(new CommandDefinition(
                "select dotacao_id, valor from sigov.orcamento_empenho_item where empenho_id=@I and not is_deleted",
                new { I = r.EmpenhoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var it in itens)
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_dotacao set empenhado=empenhado-@V, updated_at=now() where id=@D and tenant_id=@T",
                    new { V = it.Valor, D = it.DotacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
            var apuroAuto = await ParamBoolAsync(c, tx, x, "ORCAMENTO_APURO_LRF_AUTO", ct).ConfigureAwait(false);
            if (apuroAuto)
            {
                var exId = await ResoloverExercicioDeEmpenhoAsync(c, tx, r.EmpenhoId, ct).ConfigureAwait(false);
                var anoEx = await c.ExecuteScalarAsync<int>(new CommandDefinition("select ano from sigov.exercicio where id=@X", new { X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
                var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
                var linhas = await ApurarTodosAsync(c, tx, x, exId, anoEx, esfera, incAnte, ct).ConfigureAwait(false);
                await PersistirApuracoesAsync(c, tx, x, exId, esfera, clock.DataAtual, linhas, ct).ConfigureAwait(false);
            }
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23514")
        {
            throw new InvalidOperationException("anulacao de empenho geraria contador negativo (inconsistencia de dados); verifique empenhado da dotacao.", e);
        }
        _ = ano;
        return await MontarEmpenhoAsync(c, null, x, r.EmpenhoId, ct).ConfigureAwait(false);
    }

    async Task<long> ResoloverExercicioDeEmpenhoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, long empenhoId, CancellationToken ct)
    {
        return await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select exercicio_id from sigov.orcamento_empenho where id=@I", new { I = empenhoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EmpenhoDto>> ListarEmpenhosAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var rows = await c.QueryAsync<(long Id, string Numero, DateOnly Data, long? Fornecedor, string? FornecedorNome, string Tipo, string Historico, decimal Total, decimal Anulado, decimal Liquidado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select e.id, e.numero, e.data_empenho, e.fornecedor_pessoa_id, coalesce(p.nome,''), e.tipo_empenho, e.historico,
                   e.valor_total, e.valor_anulado, e.valor_liquidado, e.situacao, e.created_at
            from sigov.orcamento_empenho e left join sigov.pessoa p on p.id=e.fornecedor_pessoa_id
            where e.tenant_id=@T and e.exercicio_id=@X and not e.is_deleted order by e.id desc limit 200
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(h => new EmpenhoDto(h.Id, h.Numero, h.Data, h.Fornecedor, h.FornecedorNome, h.Tipo, h.Historico, h.Total, h.Anulado, h.Liquidado, R2(h.Total - h.Anulado - h.Liquidado), h.Sit, h.Criado, false)).ToList();
    }

    public async Task<EmpenhoDetalheDto> DetalharEmpenhoAsync(OrcamentoContext x, int ano, long empenhoId, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var h = await c.QueryFirstOrDefaultAsync<(long Id, string Numero, DateOnly Data, long? Fornecedor, string? FornecedorNome, string Tipo, string Historico, decimal Total, decimal Anulado, decimal Liquidado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select e.id, e.numero, e.data_empenho, e.fornecedor_pessoa_id, coalesce(p.nome,''), e.tipo_empenho, e.historico,
                   e.valor_total, e.valor_anulado, e.valor_liquidado, e.situacao, e.created_at
            from sigov.orcamento_empenho e left join sigov.pessoa p on p.id=e.fornecedor_pessoa_id
            where e.id=@I and e.tenant_id=@T and e.exercicio_id=@X and not e.is_deleted
            """, new { I = empenhoId, T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        if (h.Id == 0) throw new OrcamentoEmpenhoInexistenteException();
        var itens = await c.QueryAsync<(long Id, long DotacaoId, string AcaoCodigo, string AcaoNome, string Fonte, decimal Valor, decimal Anulado, decimal Liquidado)>(new CommandDefinition(
            """
            select i.id, i.dotacao_id, a.codigo, a.nome, d.fonte_recurso_codigo, i.valor, i.valor_anulado, i.valor_liquidado
            from sigov.orcamento_empenho_item i
            join sigov.orcamento_dotacao d on d.id=i.dotacao_id
            join sigov.orcamento_acao a on a.id=d.acao_id
            where i.empenho_id=@I and not i.is_deleted order by i.id
            """, new { I = empenhoId }, cancellationToken: ct)).ConfigureAwait(false);
        var lista = itens.Select(i => new EmpenhoItemDto(i.Id, i.DotacaoId, i.AcaoCodigo, i.AcaoNome, i.Fonte, i.Valor, i.Anulado, i.Liquidado, R2(i.Valor - i.Anulado - i.Liquidado))).ToList();
        return new EmpenhoDetalheDto(new EmpenhoDto(h.Id, h.Numero, h.Data, h.Fornecedor, h.FornecedorNome, h.Tipo, h.Historico, h.Total, h.Anulado, h.Liquidado, R2(h.Total - h.Anulado - h.Liquidado), h.Sit, h.Criado, false), lista);
    }

    // ----------------------------- LIQUIDACAO SL (BR-13.4) ----------------------------

    async Task<LiquidacaoDto> MontarLiquidacaoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long id, CancellationToken ct)
    {
        var h = await c.QueryFirstAsync<(long Id, string Numero, DateOnly Data, long EmpenhoId, string EmpenhoNumero, string Historico, decimal Total, decimal Anulado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select l.id, l.numero, l.data_liquidacao, l.empenho_id, e.numero, l.historico, l.valor_total, l.valor_anulado, l.situacao, l.created_at
            from sigov.orcamento_liquidacao l join sigov.orcamento_empenho e on e.id=l.empenho_id
            where l.id=@I and l.tenant_id=@T and not l.is_deleted
            """, new { I = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new LiquidacaoDto(h.Id, h.Numero, h.Data, h.EmpenhoId, h.EmpenhoNumero, h.Historico, h.Total, h.Anulado, h.Sit, h.Criado, false);
    }

    public async Task<LiquidacaoDto> RegistrarLiquidacaoAsync(OrcamentoContext x, int ano, LiquidacaoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Itens is null || r.Itens.Count == 0) throw new LiquidacaoSemItensException();
        foreach (var i in r.Itens) if (i.Valor <= 0) throw new ArgumentException($"item de liquidacao sobre item de empenho {i.EmpenhoItemId} com valor {i.Valor:0.00} deve ser > 0.");
        if (string.IsNullOrWhiteSpace(r.Numero)) throw new ArgumentException("numero e obrigatorio na liquidacao.");
        var soma = R2(r.Itens.Sum(i => i.Valor));
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        long liqId;
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            await ExigirExercicioAbertoAsync(c, tx, exId, ano, ct).ConfigureAwait(false);
            var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
            var rep = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                "select coalesce(max(id),0) from sigov.orcamento_liquidacao where tenant_id=@T and chave_idempotencia=@K and not is_deleted",
                new { T = x.TenantId, K = r.ChaveIdempotencia }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (rep != 0) return (await MontarLiquidacaoAsync(c, tx, x, rep, ct).ConfigureAwait(false)) with { Idempotente = true };
            var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
            var antes = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
            var emp = await c.QueryFirstOrDefaultAsync<(long Id, string Sit)>(new CommandDefinition(
                "select id, situacao from sigov.orcamento_empenho where id=@I and tenant_id=@T and not is_deleted for update",
                new { I = r.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (emp.Id == 0) throw new OrcamentoEmpenhoInexistenteException();
            if (emp.Sit == "ANULADO") throw new OrcamentoLiquidacaoSobreEmpenhoAnuladoException();
            var empItems = (await c.QueryAsync<(long Id, long DotacaoId, decimal Disp)>(new CommandDefinition(
                """
                select i.id, i.dotacao_id, i.valor - i.valor_anulado - i.valor_liquidado
                from sigov.orcamento_empenho_item i where i.empenho_id=@E and not i.is_deleted for update
                """, new { E = r.EmpenhoId }, tx, cancellationToken: ct)).ConfigureAwait(false)).ToDictionary(i => i.Id);
            foreach (var grp in r.Itens.GroupBy(i => i.EmpenhoItemId))
            {
                if (!empItems.TryGetValue(grp.Key, out var it)) throw new OrcamentoEmpenhoItemInexistenteException();
                var solicitado = R2(grp.Sum(g => g.Valor));
                if (solicitado > it.Disp) throw new OrcamentoLiquidacaoSuperEmpenhoException(it.Id, solicitado, it.Disp);
            }
            liqId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_liquidacao(tenant_id,entidade_id,exercicio_id,empenho_id,numero,data_liquidacao,historico,valor_total,chave_idempotencia,situacao,created_by)
                values (@T,@E,@X,@Emp,@N,@D,@H,@VT,@K,'ATIVA',@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, Emp = r.EmpenhoId, N = r.Numero.Trim(), D = r.DataLiquidacao, H = r.Historico, VT = soma, K = r.ChaveIdempotencia, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var i in r.Itens)
                await c.ExecuteAsync(new CommandDefinition(
                    "insert into sigov.orcamento_liquidacao_item(liquidacao_id,empenho_item_id,valor) values (@L,@E,@V)",
                    new { L = liqId, E = i.EmpenhoItemId, V = i.Valor }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var grp in r.Itens.GroupBy(i => i.EmpenhoItemId))
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_empenho_item set valor_liquidado=valor_liquidado+@V where id=@I",
                    new { V = R2(grp.Sum(g => g.Valor)), I = grp.Key }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_empenho set valor_liquidado=valor_liquidado+@V where id=@I and tenant_id=@T",
                new { V = soma, I = r.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var grp in r.Itens.GroupBy(i => i.EmpenhoItemId).Select(g => new { Dot = empItems[g.Key].DotacaoId, Val = g.Sum(z => z.Valor) }).GroupBy(p => p.Dot))
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_dotacao set liquidado=liquidado+@V where id=@D and tenant_id=@T",
                    new { V = R2(grp.Sum(p => p.Val)), D = grp.Key, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var depois = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
            VerificarTransicaoBloqueio(antes, depois);
            var apuroAuto = await ParamBoolAsync(c, tx, x, "ORCAMENTO_APURO_LRF_AUTO", ct).ConfigureAwait(false);
            if (apuroAuto) await PersistirApuracoesAsync(c, tx, x, exId, esfera, r.DataLiquidacao, depois, ct).ConfigureAwait(false);
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23505" && e.Message.Contains("ux_orcamento_liquidacao_chave", StringComparison.Ordinal))
        {
            var rep2 = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                "select coalesce(max(id),0) from sigov.orcamento_liquidacao where tenant_id=@T and chave_idempotencia=@K and not is_deleted",
                new { T = x.TenantId, K = r.ChaveIdempotencia }, cancellationToken: ct)).ConfigureAwait(false);
            return (await MontarLiquidacaoAsync(c, null, x, rep2, ct).ConfigureAwait(false)) with { Idempotente = true };
        }
        return (await MontarLiquidacaoAsync(c, null, x, liqId, ct).ConfigureAwait(false)) with { Idempotente = false };
    }

    public async Task<LiquidacaoDto> AnularLiquidacaoAsync(OrcamentoContext x, int ano, LiquidacaoAnularRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var minJ = await ParamIntAsync(c, tx, x, "ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES", ct).ConfigureAwait(false);
            if (minJ > 0 && (r.Justificativa?.Trim().Length ?? 0) < minJ)
                throw new OrcamentoJustificativaInsuficienteException("anulacao de liquidacao", r.Justificativa?.Trim().Length ?? 0, minJ);
            var liq = await c.QueryFirstOrDefaultAsync<(long Id, long EmpenhoId, decimal Total, decimal Anulado, string Sit)>(new CommandDefinition(
                "select id, empenho_id, valor_total, valor_anulado, situacao from sigov.orcamento_liquidacao where id=@I and tenant_id=@T and not is_deleted for update",
                new { I = r.LiquidacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (liq.Id == 0) throw new OrcamentoLiquidacaoInexistenteException();
            if (liq.Sit == "ANULADA") throw new OrcamentoLiquidacaoJaAnuladaException();
            var empLock = await c.QueryFirstOrDefaultAsync<long>(new CommandDefinition(
                "select id from sigov.orcamento_empenho where id=@I and tenant_id=@T for update",
                new { I = liq.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (empLock == 0) throw new OrcamentoEmpenhoInexistenteException();
            var porItem = (await c.QueryAsync<(long EmpenhoItemId, long DotacaoId, decimal Valor)>(new CommandDefinition(
                """
                select li.empenho_item_id, ei.dotacao_id, sum(li.valor)
                from sigov.orcamento_liquidacao_item li join sigov.orcamento_empenho_item ei on ei.id=li.empenho_item_id
                where li.liquidacao_id=@L and not li.is_deleted group by li.empenho_item_id, ei.dotacao_id
                """, new { L = r.LiquidacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false)).ToList();
            foreach (var g in porItem.GroupBy(p => p.EmpenhoItemId))
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_empenho_item set valor_liquidado=valor_liquidado-@V where id=@I",
                    new { V = R2(g.Sum(p => p.Valor)), I = g.Key }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var g in porItem.GroupBy(p => p.DotacaoId))
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.orcamento_dotacao set liquidado=liquidado-@V where id=@D and tenant_id=@T",
                    new { V = R2(g.Sum(p => p.Valor)), D = g.Key, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_empenho set valor_liquidado=valor_liquidado-@V where id=@I and tenant_id=@T",
                new { V = R2(liq.Total - liq.Anulado), I = liq.EmpenhoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_liquidacao set situacao='ANULADA', valor_anulado=valor_total, anulada_em=now(), anulada_por=@U, anulacao_justificativa=@J, updated_at=now() where id=@I and tenant_id=@T",
                new { U = U(x), J = r.Justificativa?.Trim() ?? "", I = r.LiquidacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
            var apuroAuto = await ParamBoolAsync(c, tx, x, "ORCAMENTO_APURO_LRF_AUTO", ct).ConfigureAwait(false);
            if (apuroAuto)
            {
                var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
                var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
                var linhas = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
                await PersistirApuracoesAsync(c, tx, x, exId, esfera, clock.DataAtual, linhas, ct).ConfigureAwait(false);
            }
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23514")
        {
            throw new InvalidOperationException("anulacao de liquidacao geraria contador negativo (inconsistencia de dados); verifique o empenho.", e);
        }
        return (await MontarLiquidacaoAsync(c, null, x, r.LiquidacaoId, ct).ConfigureAwait(false)) with { Idempotente = false };
    }

    public async Task<IReadOnlyList<LiquidacaoDto>> ListarLiquidacoesAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var rows = await c.QueryAsync<(long Id, string Numero, DateOnly Data, long EmpenhoId, string EmpenhoNumero, string Historico, decimal Total, decimal Anulado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select l.id, l.numero, l.data_liquidacao, l.empenho_id, e.numero, l.historico, l.valor_total, l.valor_anulado, l.situacao, l.created_at
            from sigov.orcamento_liquidacao l join sigov.orcamento_empenho e on e.id=l.empenho_id
            where l.tenant_id=@T and l.exercicio_id=@X and not l.is_deleted order by l.id desc limit 200
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(h => new LiquidacaoDto(h.Id, h.Numero, h.Data, h.EmpenhoId, h.EmpenhoNumero, h.Historico, h.Total, h.Anulado, h.Sit, h.Criado, false)).ToList();
    }

    public async Task<LiquidacaoDetalheDto> DetalharLiquidacaoAsync(OrcamentoContext x, int ano, long liquidacaoId, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var h = await c.QueryFirstOrDefaultAsync<(long Id, string Numero, DateOnly Data, long EmpenhoId, string EmpenhoNumero, string Historico, decimal Total, decimal Anulado, string Sit, DateTime Criado)>(new CommandDefinition(
            """
            select l.id, l.numero, l.data_liquidacao, l.empenho_id, e.numero, l.historico, l.valor_total, l.valor_anulado, l.situacao, l.created_at
            from sigov.orcamento_liquidacao l join sigov.orcamento_empenho e on e.id=l.empenho_id
            where l.id=@I and l.tenant_id=@T and l.exercicio_id=@X and not l.is_deleted
            """, new { I = liquidacaoId, T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        if (h.Id == 0) throw new OrcamentoLiquidacaoInexistenteException();
        var itens = await c.QueryAsync<(long Id, long EmpenhoItemId, string AcaoCodigo, string AcaoNome, string Fonte, decimal Valor)>(new CommandDefinition(
            """
            select li.id, li.empenho_item_id, a.codigo, a.nome, d.fonte_recurso_codigo, li.valor
            from sigov.orcamento_liquidacao_item li
            join sigov.orcamento_empenho_item ei on ei.id=li.empenho_item_id
            join sigov.orcamento_dotacao d on d.id=ei.dotacao_id
            join sigov.orcamento_acao a on a.id=d.acao_id
            where li.liquidacao_id=@L and not li.is_deleted order by li.id
            """, new { L = liquidacaoId }, cancellationToken: ct)).ConfigureAwait(false);
        var lista = itens.Select(i => new LiquidacaoItemDto(i.Id, i.EmpenhoItemId, i.AcaoCodigo, i.AcaoNome, i.Fonte, i.Valor)).ToList();
        return new LiquidacaoDetalheDto(new LiquidacaoDto(h.Id, h.Numero, h.Data, h.EmpenhoId, h.EmpenhoNumero, h.Historico, h.Total, h.Anulado, h.Sit, h.Criado, false), lista);
    }

    // ----------------------------- RECEITA (base real) --------------------------------

    async Task<ReceitaPrevistaDto> MontarReceitaAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long id, bool idempotente, CancellationToken ct)
    {
        var r = await c.QueryFirstAsync<(long Id, string Cod, string Nome, string Nat, decimal Prev, decimal Arrec)>(new CommandDefinition(
            "select id, codigo, nome, natureza, valor_prevista, valor_arrecadado from sigov.orcamento_receita_prevista where id=@I and tenant_id=@T and not is_deleted",
            new { I = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new ReceitaPrevistaDto(r.Id, r.Cod, r.Nome, r.Nat, r.Prev, r.Arrec, R2(r.Prev - r.Arrec), idempotente);
    }

    public async Task<ReceitaPrevistaDto> CriarReceitaAsync(OrcamentoContext x, int ano, ReceitaCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) throw new ArgumentException("codigo e nome sao obrigatorios na receita prevista.");
        if (!NaturezasReceita.Contains(r.Natureza, StringComparer.Ordinal)) throw new OrcamentoReceitaNaturezaInvalidaException(r.Natureza ?? "");
        if (r.ValorPrevisto < 0) throw new ArgumentException($"receita prevista {r.ValorPrevisto:0.00} nao pode ser negativa.");
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_receita_prevista(tenant_id,entidade_id,exercicio_id,codigo,nome,natureza,valor_prevista,created_by)
                values (@T,@E,@X,@C,@N,@Nat,@V,@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, C = r.Codigo.Trim(), N = r.Nome.Trim(), Nat = r.Natureza, V = R2(r.ValorPrevisto), U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return await MontarReceitaAsync(c, null, x, id, false, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            throw new OrcamentoReceitaJaExisteException(r.Codigo.Trim());
        }
    }

    public async Task<ReceitaPrevistaDto> ArrecadarReceitaAsync(OrcamentoContext x, int ano, ReceitaArrecadarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Valor <= 0) throw new OrcamentoArrecadacaoInvalidaException(r.Valor);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var rec = await c.QueryFirstOrDefaultAsync<(long Id, long ExId, int Ano)>(new CommandDefinition(
                "select id, exercicio_id, (select ano from sigov.exercicio e2 where e2.id=orcamento_receita_prevista.exercicio_id) from sigov.orcamento_receita_prevista where id=@I and tenant_id=@T and not is_deleted for update",
                new { I = r.ReceitaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (rec.Id == 0) throw new OrcamentoReceitaInexistenteException();
            var rep = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(1) from sigov.orcamento_receita_arrecadacao where tenant_id=@T and chave_idempotencia=@K",
                new { T = x.TenantId, K = r.ChaveIdempotencia }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (rep > 0) return await MontarReceitaAsync(c, tx, x, rec.Id, true, ct).ConfigureAwait(false);
            var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
            var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
            var antes = await ApurarTodosAsync(c, tx, x, rec.ExId, rec.Ano, esfera, incAnte, ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_receita_prevista set valor_arrecadado=valor_arrecadado+@V, updated_at=now() where id=@I and tenant_id=@T",
                new { V = R2(r.Valor), I = r.ReceitaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.orcamento_receita_arrecadacao(tenant_id,receita_id,valor,data_arrecadacao,historico,chave_idempotencia,criado_por)
                values (@T,@R,@V,@D,@H,@K,@U)
                """, new { T = x.TenantId, R = r.ReceitaId, V = R2(r.Valor), D = r.DataArrecadacao, H = r.Historico, K = r.ChaveIdempotencia, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var depois = await ApurarTodosAsync(c, tx, x, rec.ExId, rec.Ano, esfera, incAnte, ct).ConfigureAwait(false);
            VerificarTransicaoBloqueio(antes, depois);
            var apuroAuto = await ParamBoolAsync(c, tx, x, "ORCAMENTO_APURO_LRF_AUTO", ct).ConfigureAwait(false);
            if (apuroAuto) await PersistirApuracoesAsync(c, tx, x, rec.ExId, esfera, r.DataArrecadacao, depois, ct).ConfigureAwait(false);
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23505" && e.Message.Contains("ux_orcamento_arrecadacao_chave", StringComparison.Ordinal))
        {
            return await MontarReceitaAsync(c, null, x, r.ReceitaId, true, ct).ConfigureAwait(false);
        }
        return await MontarReceitaAsync(c, null, x, r.ReceitaId, false, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReceitaPrevistaDto>> ListarReceitasAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var rows = await c.QueryAsync<(long Id, string Cod, string Nome, string Nat, decimal Prev, decimal Arrec)>(new CommandDefinition(
            "select id, codigo, nome, natureza, valor_prevista, valor_arrecadado from sigov.orcamento_receita_prevista where tenant_id=@T and exercicio_id=@X and not is_deleted order by id",
            new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new ReceitaPrevistaDto(r.Id, r.Cod, r.Nome, r.Nat, r.Prev, r.Arrec, R2(r.Prev - r.Arrec), false)).ToList();
    }

    // ----------------------------- LRF: LIMITES E APUURACOES (BR-13.5) ------------------

    public async Task<IReadOnlyList<LimiteLrfDto>> LimitesEfetivosAsync(OrcamentoContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var esfera = await EsferaAsync(c, null, x, ct).ConfigureAwait(false);
        var rows = await c.QueryAsync<(string Chave, string Nome, string Tipo, decimal Alvo, decimal Alerta, decimal? Bloq, string Base, bool Ativo, long? Tenant)>(new CommandDefinition(
            """
            select l.chave, l.nome, l.tipo_comparacao as tipo, l.valor_alvo as alvo, l.alerta_pct as alerta, l.bloqueio_pct as bloq, l.base_receita as "baseReceita", l.ativo, l.tenant_id
            from sigov.orcamento_limite_lrf l
            where l.esfera_governo=@Esf and l.ativo and not l.is_deleted
              and (l.tenant_id is null or l.tenant_id=@T)
            order by l.tenant_id nulls last, l.chave
            """, new { Esf = esfera, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.GroupBy(r => r.Chave).Select(g =>
        {
            var r = g.First();
            return new LimiteLrfDto(r.Chave, r.Nome, r.Tipo, r.Alvo, r.Alerta, r.Bloq, r.Base, r.Tenant is null ? "INSTITUCIONAL" : "OVERRIDE_TENANT", r.Ativo);
        }).ToList();
    }

    public async Task<IReadOnlyList<LrfApuracaoDto>> ApurarLrfAsync(OrcamentoContext x, int ano, LrfApurarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
        var esfera = await EsferaAsync(c, tx, x, ct).ConfigureAwait(false);
        var incAnte = await ParamBoolAsync(c, tx, x, "ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES", ct).ConfigureAwait(false);
        var linhas = await ApurarTodosAsync(c, tx, x, exId, ano, esfera, incAnte, ct).ConfigureAwait(false);
        await PersistirApuracoesAsync(c, tx, x, exId, esfera, r.Periodo, linhas, ct).ConfigureAwait(false);
        tx.Commit();
        return await ListarApuracoesSqlAsync(c, null, x, exId, ct).ConfigureAwait(false);
    }

    async Task<IReadOnlyList<LrfApuracaoDto>> ListarApuracoesSqlAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long exId, CancellationToken ct)
    {
        var rows = await c.QueryAsync<LrfApuracaoDto>(new CommandDefinition(
            """
            select a.id, a.periodo, a.limite_chave as "limiteChave", coalesce(l.nome, a.limite_chave) as nome, a.base_valor as "baseValor", a.valor_atual as "valorAtual", a.percentual, a.situacao, a.detalhe, a.versao, a.criado_em as "criadoEm"
            from sigov.orcamento_lrf_apuracao a
            left join sigov.orcamento_limite_lrf l on l.chave=a.limite_chave and l.esfera_governo=a.esfera_governo and l.tenant_id is null
            where a.tenant_id=@T and a.exercicio_id=@X
              and a.versao=(select coalesce(max(a2.versao),0) from sigov.orcamento_lrf_apuracao a2 where a2.tenant_id=@T and a2.exercicio_id=@X and a2.limite_chave=a.limite_chave)
            order by a.limite_chave
            """, new { T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LrfApuracaoDto>> ListarApuracoesAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        return await ListarApuracoesSqlAsync(c, null, x, exId, ct).ConfigureAwait(false);
    }

    // ----------------------------- PREVISAO BIMESTRAL (BR-13.6) --------------------------

    static async Task<(decimal RecPrev, decimal RecArrec, int LinRec, decimal Autorizado, decimal Empenhado, decimal Liquidado, int LinDot)> TotaisExercicioAsync(NpgsqlConnection c, NpgsqlTransaction? tx, long tenantId, long exId, CancellationToken ct)
    {
        var (recPrev, recArrec, linRec) = await c.QuerySingleAsync<(decimal A, decimal B, int C)>(new CommandDefinition(
            """
            select coalesce(sum(valor_prevista),0), coalesce(sum(valor_arrecadado),0), count(1)
            from sigov.orcamento_receita_prevista where tenant_id=@T and exercicio_id=@X and not is_deleted
            """, new { T = tenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var (aut, emp, liq, linDot) = await c.QuerySingleAsync<(decimal A, decimal B, decimal C, int D)>(new CommandDefinition(
            """
            select coalesce(sum(dotacao_inicial+creditos_suplementares+creditos_especiais+creditos_extraordinarios-reducoes),0),
                   coalesce(sum(empenhado),0), coalesce(sum(liquidado),0), count(1)
            from sigov.orcamento_dotacao where tenant_id=@T and exercicio_id=@X and not is_deleted
            """, new { T = tenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return (R2(recPrev), R2(recArrec), linRec, R2(aut), R2(emp), R2(liq), linDot);
    }

    public async Task<PrevisaoBimestralDto> GerarPrevisaoBimestralAsync(OrcamentoContext x, int ano, PrevisaoGerarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Bimestre < 1 || r.Bimestre > 6) throw new OrcamentoBimestreInvalidoException(r.Bimestre);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        long novoId;
        try
        {
            var (exId, _, _) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            var t = await TotaisExercicioAsync(c, tx, x.TenantId, exId, ct).ConfigureAwait(false);
            var saldo = R2(t.RecPrev - t.RecArrec);
            var fonte = JsonSerializer.Serialize(new { ano, bimestre = r.Bimestre, linhas_receita = t.LinRec, linhas_dotacao = t.LinDot, gerado_por = U(x) });
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_previsao_bimestral set vigente=false where tenant_id=@T and entidade_id=@E and exercicio_id=@X and bimestre=@B and vigente",
                new { T = x.TenantId, E = Ent(x), X = exId, B = r.Bimestre }, tx, cancellationToken: ct)).ConfigureAwait(false);
            novoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_previsao_bimestral(tenant_id,entidade_id,exercicio_id,bimestre,receita_prevista_total,receita_arrecadada_total,despesa_autorizada_total,despesa_empenhada_total,despesa_liquidada_total,saldo_receitas,dados_fonte,vigente,gerado_por,observacao)
                values (@T,@E,@X,@B,@RP,@RA,@DA,@DE,@DL,@SR,@DF::jsonb,true,@U,@O) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, B = r.Bimestre, RP = t.RecPrev, RA = t.RecArrec, DA = t.Autorizado, DE = t.Empenhado, DL = t.Liquidado, SR = saldo, DF = fonte, U = U(x), O = r.Observacao }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            throw new InvalidOperationException("previsao bimestral vigente ja existe para o bimestre; rode novamente (substitui a anterior).", e);
        }
        return (await c.QueryFirstAsync<PrevisaoBimestralDto>(new CommandDefinition(
            """
            select id, bimestre, receita_prevista_total as "receitaPrevistaTotal", receita_arrecadada_total as "receitaArrecadadaTotal", despesa_autorizada_total as "despesaAutorizadaTotal", despesa_empenhada_total as "despesaEmpenhadaTotal", despesa_liquidada_total as "despesaLiquidadaTotal",
                   saldo_receitas as "saldoReceitas", vigente, gerado_em as "geradoEm", observacao
            from sigov.orcamento_previsao_bimestral where id=@I and tenant_id=@T
            """, new { I = novoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<PrevisaoBimestralDto>> ListarPrevisoesAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        return (await c.QueryAsync<PrevisaoBimestralDto>(new CommandDefinition(
            """
            select id, bimestre, receita_prevista_total as "receitaPrevistaTotal", receita_arrecadada_total as "receitaArrecadadaTotal", despesa_autorizada_total as "despesaAutorizadaTotal", despesa_empenhada_total as "despesaEmpenhadaTotal", despesa_liquidada_total as "despesaLiquidadaTotal",
                   saldo_receitas as "saldoReceitas", vigente, gerado_em as "geradoEm", observacao
            from sigov.orcamento_previsao_bimestral where tenant_id=@T and exercicio_id=@X order by bimestre, id desc
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
    }

    // ----------------------------- RREO (BR-13.7) ----------------------------------------

    static readonly (string Conjunto, string Rotulo)[] ConjuntosRreo = {
        ("RECEITA_PREVISTA_GERAL", "Receita prevista geral"),
        ("RECEITA_ARRECADADA", "Receita arrecadada"),
        ("SALDO_RECEITAS", "Saldo de receitas"),
        ("DESPESA_AUTORIZADA", "Despesa autorizada"),
        ("DESPESA_EMPENHADA", "Despesa empenhada"),
        ("DESPESA_LIQUIDADA", "Despesa liquidada"),
        ("RESTOS_PAGAR_EXERCICIO_ATUAL", "Restos a pagar do exercicio atual (empenhos nao liquidados)"),
        ("RESTOS_PAGAR_EXERCICIOS_ANTERIORES", "Restos a pagar de exercicios anteriores (empenhos nao liquidados)"),
        ("LIQUIDACAO_RESTOS_NO_EXERCICIO", "Restos a pagar de exercicios anteriores liquidados neste exercicio"),
    };

    public async Task<RreoLoteDto> GerarRreoAsync(OrcamentoContext x, int ano, RreoGerarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Tipo is not ("SEMESTRAL" or "ANUAL")) throw new OrcamentoRreoTipoInvalidoException(r.Tipo ?? "");
        if (r.Tipo == "SEMESTRAL" && (r.Semestre is not (1 or 2))) throw new OrcamentoSemestreRreoInvalidoException(r.Tipo, r.Semestre);
        if (r.Tipo == "ANUAL" && r.Semestre.HasValue) throw new OrcamentoSemestreRreoInvalidoException(r.Tipo, r.Semestre);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var (exId, ini, fim) = await ResoloverExercicioAsync(c, tx, x, ano, ct).ConfigureAwait(false);
            if (r.PeriodoFim < ini || r.PeriodoFim > fim) throw new OrcamentoRreoPeriodoInconsistenteException(ano, r.PeriodoFim);
            var t = await TotaisExercicioAsync(c, tx, x.TenantId, exId, ct).ConfigureAwait(false);
            var raAtual = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                select coalesce(sum(e.valor_total-e.valor_anulado-e.valor_liquidado),0)
                from sigov.orcamento_empenho e where e.tenant_id=@T and e.exercicio_id=@X and e.situacao='ATIVO' and not e.is_deleted
                """, new { T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var raAntes = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                select coalesce(sum(e.valor_total-e.valor_anulado-e.valor_liquidado),0)
                from sigov.orcamento_empenho e join sigov.exercicio ex on ex.id=e.exercicio_id
                where e.tenant_id=@T and e.situacao='ATIVO' and not e.is_deleted and ex.entidade_id=@E and ex.ano<@A
                """, new { T = x.TenantId, E = Ent(x), A = ano }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var raLiq = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                select coalesce(sum(l2.valor_total-l2.valor_anulado),0)
                from sigov.orcamento_liquidacao l2 join sigov.orcamento_empenho e2 on e2.id=l2.empenho_id
                where l2.tenant_id=@T and l2.exercicio_id=@X and l2.situacao='ATIVA' and not l2.is_deleted and e2.exercicio_id<>l2.exercicio_id
                """, new { T = x.TenantId, X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var valores = new Dictionary<string, decimal> {
                ["RECEITA_PREVISTA_GERAL"] = R2(t.RecPrev), ["RECEITA_ARRECADADA"] = R2(t.RecArrec),
                ["SALDO_RECEITAS"] = R2(t.RecPrev - t.RecArrec), ["DESPESA_AUTORIZADA"] = R2(t.Autorizado),
                ["DESPESA_EMPENHADA"] = R2(t.Empenhado), ["DESPESA_LIQUIDADA"] = R2(t.Liquidado),
                ["RESTOS_PAGAR_EXERCICIO_ATUAL"] = R2(raAtual), ["RESTOS_PAGAR_EXERCICIOS_ANTERIORES"] = R2(raAntes),
                ["LIQUIDACAO_RESTOS_NO_EXERCICIO"] = R2(raLiq),
            };
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_rreo_lote set vigente=false where tenant_id=@T and entidade_id=@E and exercicio_id=@X and tipo=@Tp and coalesce(semestre,0)=coalesce(@S,0) and vigente",
                new { T = x.TenantId, E = Ent(x), X = exId, Tp = r.Tipo, S = r.Semestre }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var loteId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.orcamento_rreo_lote(tenant_id,entidade_id,exercicio_id,tipo,semestre,periodo_fim,vigente,gerado_por)
                values (@T,@E,@X,@Tp,@S,@F,true,@U) returning id
                """, new { T = x.TenantId, E = Ent(x), X = exId, Tp = r.Tipo, S = r.Semestre, F = r.PeriodoFim, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var (conj, rotulo) in ConjuntosRreo)
                await c.ExecuteAsync(new CommandDefinition(
                    "insert into sigov.orcamento_rreo_item(lote_id,conjunto,valor,detalhe) values (@L,@C,@V,@R)",
                    new { L = loteId, C = conj, V = valores[conj], R = rotulo }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return (await c.QueryFirstAsync<RreoLoteDto>(new CommandDefinition(
                "select id, tipo, semestre, periodo_fim as \"periodoFim\", vigente, gerado_em as \"geradoEm\" from sigov.orcamento_rreo_lote where id=@I and tenant_id=@T",
                new { I = loteId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false));
        }
        catch (NpgsqlException e) when (e.SqlState == "23505")
        {
            throw new InvalidOperationException("lote RREO vigente ja existe para o periodo; rode novamente (substitui o anterior).", e);
        }
    }

    public async Task<IReadOnlyList<RreoLoteDto>> ListarRreosAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        return (await c.QueryAsync<RreoLoteDto>(new CommandDefinition(
            "select id, tipo, semestre, periodo_fim as \"periodoFim\", vigente, gerado_em as \"geradoEm\" from sigov.orcamento_rreo_lote where tenant_id=@T and exercicio_id=@X order by id desc limit 200",
            new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
    }

    public async Task<RreoLoteDetalheDto> DetalharRreoAsync(OrcamentoContext x, int ano, long loteId, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var lote = await c.QueryFirstOrDefaultAsync<RreoLoteDto>(new CommandDefinition(
            "select id, tipo, semestre, periodo_fim as \"periodoFim\", vigente, gerado_em as \"geradoEm\" from sigov.orcamento_rreo_lote where id=@I and tenant_id=@T and exercicio_id=@X",
            new { I = loteId, T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        if (lote == null) throw new OrcamentoRreoInexistenteException();
        var itens = await c.QueryAsync<(string Conjunto, string Rotulo, decimal Valor)>(new CommandDefinition(
            "select i.conjunto, i.detalhe, i.valor from sigov.orcamento_rreo_item i where i.lote_id=@L order by i.id",
            new { L = loteId }, cancellationToken: ct)).ConfigureAwait(false);
        // rotulo persistido em detalhe na geracao; valores sao os do banco.
        var conjuntos = itens.Select(i => new RreoItemDto(i.Conjunto, string.IsNullOrWhiteSpace(i.Rotulo) ? i.Conjunto : i.Rotulo, i.Valor, "")).ToList();
        return new RreoLoteDetalheDto(lote, conjuntos);
    }

    // ----------------------------- COMPETENCIA FECHADA (BR-13.8) -------------------------

    static async Task<EstadoExercicioDto> MontarEstadoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, OrcamentoContext x, long exId, CancellationToken ct)
    {
        var r = await c.QueryFirstAsync<(int Ano, string Sit, DateTime? EncEm, string? EncMotivo, DateTime? ReaEm, string? ReaJust)>(new CommandDefinition(
            """
            select ex.ano, coalesce(st.situacao,'ABERTO'), st.encerrado_em, st.encerramento_motivo, st.reaberto_em, st.reabertura_justificativa
            from sigov.exercicio ex left join sigov.orcamento_estado_exercicio st on st.exercicio_id=ex.id and not st.is_deleted
            where ex.id=@X
            """, new { X = exId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new EstadoExercicioDto(exId, r.Ano, r.Sit, r.EncEm, r.EncMotivo, r.ReaEm, r.ReaJust);
    }

    public async Task<IReadOnlyList<EstadoExercicioDto>> ListarExerciciosAsync(OrcamentoContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var e = Ent(x);
        return (await c.QueryAsync<EstadoExercicioDto>(new CommandDefinition(
            """
            select ex.id as "exercicioId", ex.ano, coalesce(st.situacao,'ABERTO') as situacao, st.encerrado_em as "encerradoEm", st.encerramento_motivo as "encerramentoMotivo", st.reaberto_em as "reabertoEm", st.reabertura_justificativa as "reaberturaJustificativa"
            from sigov.exercicio ex
            left join sigov.orcamento_estado_exercicio st on st.exercicio_id=ex.id and not st.is_deleted
            where ex.entidade_id=@E and not ex.is_deleted order by ex.ano desc limit 200
            """, new { E = e }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
    }

    public async Task<EstadoExercicioDto> EncerrarExercicioAsync(OrcamentoContext x, ExercicioEncerrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Motivo)) throw new OrcamentoJustificativaInsuficienteException("encerramento de exercicio", r.Motivo?.Trim().Length ?? 0, 1);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var ex = await c.QueryFirstOrDefaultAsync<(long Id, int Ano, long EntidadeId)>(new CommandDefinition(
                "select id, ano, entidade_id from sigov.exercicio where id=@I and not is_deleted",
                new { I = r.ExercicioId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (ex.Id == 0 || ex.EntidadeId != Ent(x)) throw new OrcamentoExercicioInexistenteException(ex.Ano);
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.orcamento_estado_exercicio(tenant_id,entidade_id,exercicio_id,situacao,encerrado_em,encerrado_por,encerramento_motivo)
                values (@T,@E,@X,'ENCERRADO',now(),@U,@M)
                on conflict (exercicio_id) where not is_deleted
                do update set situacao='ENCERRADO', encerrado_em=now(), encerrado_por=@U2, encerramento_motivo=@M, updated_at=now()
                """, new { T = x.TenantId, E = Ent(x), X = r.ExercicioId, U = U(x), U2 = U(x), M = r.Motivo.Trim() }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return await MontarEstadoAsync(c, null, x, r.ExercicioId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException e) when (e.SqlState == "40P01")
        {
            throw new InvalidOperationException("estado do exercicio em conflito concorrente; tente novamente.", e);
        }
    }

    public async Task<EstadoExercicioDto> ReabrirExercicioAsync(OrcamentoContext x, ExercicioReabrirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        using var tx = BeginTx(c);
        try
        {
            var minJ = await ParamIntAsync(c, tx, x, "ORCAMENTO_REABERTURA_JUSTIFICATIVA_MIN_CARACTERES", ct).ConfigureAwait(false);
            if (minJ > 0 && (r.Justificativa?.Trim().Length ?? 0) < minJ)
                throw new OrcamentoJustificativaInsuficienteException("reabertura de exercicio", r.Justificativa?.Trim().Length ?? 0, minJ);
            var st = await c.QueryFirstOrDefaultAsync<(long Id, string Sit)>(new CommandDefinition(
                "select id, situacao from sigov.orcamento_estado_exercicio where exercicio_id=@X and not is_deleted for update",
                new { X = r.ExercicioId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (st.Id == 0 || st.Sit != "ENCERRADO") throw new OrcamentoExercicioNaoEstavaEncerradoException();
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.orcamento_estado_exercicio set situacao='ABERTO', reaberto_em=now(), reaberto_por=@U, reabertura_justificativa=@J, updated_at=now() where id=@I",
                new { U = U(x), J = r.Justificativa?.Trim() ?? "", I = st.Id }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
            return await MontarEstadoAsync(c, null, x, r.ExercicioId, ct).ConfigureAwait(false);
        }
        catch (NpgsqlException e) when (e.SqlState == "40P01")
        {
            throw new InvalidOperationException("estado do exercicio em conflito concorrente; tente novamente.", e);
        }
    }

    // ----------------------------- PARAMETROS E DASHBOARD --------------------------------

    static string JsonTextoEfetivo(string v) => (v.Length >= 2 && v.StartsWith('"') && v.EndsWith('"')) ? v.Substring(1, v.Length - 2) : v;

    public async Task<IReadOnlyList<ParametroEfetivoDto>> ParametrosEfetivosAsync(OrcamentoContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var rows = await c.QueryAsync<(string Cod, string Nome, string Tipo, string Desc, string Padrao, string? Ov)>(new CommandDefinition(
            """
            select p.codigo, p.nome, p.tipo, p.descricao, p.valor_padrao::text,
                   (select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=p.id and not val.is_deleted limit 1)
            from sigov.parametro_modulo p where p.modulo='ORCAMENTO' and p.ativo and not p.is_deleted order by p.ordem
            """, new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        // Representacao ::text de jsonb preserva aspas para strings; o valor efetivo exposto
        // deve ser identico ao lido pelo motor (que ja faz o strip nas aspas).
        return rows.Select(r => new ParametroEfetivoDto(r.Cod, r.Nome, r.Tipo, JsonTextoEfetivo(r.Ov ?? r.Padrao), r.Ov is null ? "PADRAO" : "OVERRIDE", r.Desc)).ToList();
    }

    public async Task<DashboardOrcamentoDto> DashboardAsync(OrcamentoContext x, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var (exId, _, _) = await ResoloverExercicioAsync(c, null, x, ano, ct).ConfigureAwait(false);
        var esfera = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select esfera_governo from sigov.entidade where id=@E and tenant_id=@T",
            new { E = Ent(x), T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        var (acoes, dots, emps, liqs) = await c.QuerySingleAsync<(int A, int B, int C, int D)>(new CommandDefinition(
            """
            select
              (select count(1) from sigov.orcamento_acao where tenant_id=@T and exercicio_id=@X and ativo and not is_deleted),
              (select count(1) from sigov.orcamento_dotacao where tenant_id=@T and exercicio_id=@X and not is_deleted),
              (select count(1) from sigov.orcamento_empenho where tenant_id=@T and exercicio_id=@X and situacao='ATIVO' and not is_deleted),
              (select count(1) from sigov.orcamento_liquidacao where tenant_id=@T and exercicio_id=@X and situacao='ATIVA' and not is_deleted)
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        var (aut, emp, liq) = await c.QuerySingleAsync<(decimal A, decimal B, decimal C)>(new CommandDefinition(
            """
            select coalesce(sum(dotacao_inicial+creditos_suplementares+creditos_especiais+creditos_extraordinarios-reducoes),0),
                   coalesce(sum(empenhado),0), coalesce(sum(liquidado),0)
            from sigov.orcamento_dotacao where tenant_id=@T and exercicio_id=@X and not is_deleted
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        var disp = R2(aut - emp);
        var (rp, ra) = await c.QuerySingleAsync<(decimal A, decimal B)>(new CommandDefinition(
            "select coalesce(sum(valor_prevista),0), coalesce(sum(valor_arrecadado),0) from sigov.orcamento_receita_prevista where tenant_id=@T and exercicio_id=@X and not is_deleted",
            new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        var (alertas, bloqueios) = await c.QuerySingleAsync<(int A, int B)>(new CommandDefinition(
            """
            select coalesce(sum(case when a.situacao='ALERTA' then 1 else 0 end),0), coalesce(sum(case when a.situacao='BLOQUEIO' then 1 else 0 end),0)
            from sigov.orcamento_lrf_apuracao a
            where a.tenant_id=@T and a.exercicio_id=@X
              and a.versao=(select coalesce(max(a2.versao),0) from sigov.orcamento_lrf_apuracao a2 where a2.tenant_id=@T and a2.exercicio_id=@X and a2.limite_chave=a.limite_chave)
            """, new { T = x.TenantId, X = exId }, cancellationToken: ct)).ConfigureAwait(false);
        return new DashboardOrcamentoDto(esfera, acoes, dots, emps, liqs, R2(aut), R2(emp), R2(liq), disp, R2(rp), R2(ra), alertas, bloqueios);
    }
}
