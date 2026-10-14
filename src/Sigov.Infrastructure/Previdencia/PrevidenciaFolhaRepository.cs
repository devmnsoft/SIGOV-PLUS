using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Health;
using Sigov.Application.Rh.Previdencia;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Previdencia;

// RC-EVO Ciclo 2 Sprint 12 (BR-12.1 a BR-12.8): motor de folha e previdencia publica
// deterministico e multi-esfera. Banco = autoridade (regras 11/12): faixas INSS/RPPS por
// regime+ano, tabela progressiva IRRF por ano-base, teto constitucional por esfera, prazos de
// ferias/13o/afastamentos — tudo em tabelas/parametros, nada hardcoded no codigo. A data base
// do calculo (DataFixa) e explicita por comando (auditavel e deterministica para homologacao).
// Falhas nomeadas explicitas (regra 13): schema ausente -> PREVIDENCIA_NAO_DISPONIVEL.
public sealed class PrevidenciaFolhaRepository(NpgsqlConnectionFactory factory, IDatabaseObjectInspector inspector, IDateTimeProvider clock) : IPrevidenciaFolhaService
{
    static readonly string[] Tabelas = {
        "previdencia_natureza","previdencia_cargo","previdencia_servidor","previdencia_vinculo",
        "previdencia_remun_item","previdencia_folha_competencia","previdencia_folha_calculo",
        "previdencia_folha_calculo_item","previdencia_ferias","previdencia_ferias_programacao",
        "previdencia_afastamento","previdencia_decimo","previdencia_esocial_lote","previdencia_esocial_evento",
        "previdencia_inss_faixa","previdencia_irrf_faixa"
    };

    async Task GuardAsync(CancellationToken ct)
    {
        foreach (var t in Tabelas)
            if (!await inspector.TableExistsAsync("sigov", t, ct).ConfigureAwait(false))
                throw new PrevidenciaRecursoNaoDisponivelException(t);
    }

    long U(PrevidenciaContext x) => x.UsuarioId ?? throw new InvalidOperationException("usuário obrigatório no contexto.");

    static string SqlParametro =>
        """
        select coalesce((select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=c.pid and not val.is_deleted limit 1), c.vp)
        from (select id as pid, valor_padrao::text as vp from sigov.parametro_modulo where modulo='PREVIDENCIA' and codigo=@Cod and ativo and not is_deleted limit 1) c
        """;

    async Task<string> ParamTextoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, string codigo, CancellationToken ct)
    {
        var v = await c.ExecuteScalarAsync<string>(new CommandDefinition(SqlParametro, new { T = x.TenantId, Cod = codigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v)) throw new PrevidenciaRecursoNaoDisponivelException("parametro_modulo.PREVIDENCIA." + codigo);
        return v.Trim().Trim('"');
    }

    async Task<int> ParamIntAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, string codigo, CancellationToken ct)
    {
        var v = await ParamTextoAsync(c, tx, x, codigo, ct).ConfigureAwait(false);
        if (!int.TryParse(v, out var n)) throw new PrevidenciaRecursoNaoDisponivelException("parametro_modulo.PREVIDENCIA." + codigo + "=" + v);
        return n;
    }

    async Task<decimal> ParamDecimalAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, string codigo, CancellationToken ct)
    {
        var v = await ParamTextoAsync(c, tx, x, codigo, ct).ConfigureAwait(false);
        if (!decimal.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out var d)) throw new PrevidenciaRecursoNaoDisponivelException("parametro_modulo.PREVIDENCIA." + codigo + "=" + v);
        return d;
    }

    // Regras 21-24: a esfera vem da entidade do contexto institucional; ausente = falha explicita.
    async Task<string> EsferaAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, CancellationToken ct)
    {
        var e = x.EntidadeId ?? throw new EntidadeSemEsferaException();
        var s = await c.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            select esfera_governo from sigov.entidade where id=@E and tenant_id=@T and ativo=true and not is_deleted
            """, new { E = e, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(s)) throw new EntidadeSemEsferaException();
        return s.Trim().ToLowerInvariant();
    }

    static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    // Meses do ano com pelo menos 15 dias trabalhados (Lei 4.749/65: 1/12 por mes >= 15 dias).
    static int MesesCom15Dias(DateOnly admissao, DateOnly datafixa, int ano, int ateMes)
    {
        var n = 0;
        for (var m = 1; m <= ateMes; m++)
        {
            var inicioMes = new DateOnly(ano, m, 1);
            var fimMes = inicioMes.AddMonths(1).AddDays(-1);
            var a = admissao > inicioMes ? admissao : inicioMes;
            var b = datafixa < fimMes ? datafixa : fimMes;
            if (b >= a && (b.DayNumber - a.DayNumber + 1) >= 15) n++;
        }
        return Math.Min(n, 12);
    }

    sealed record ItemBase(string NaturezaCodigo, string Nome, decimal Valor, bool IncidePrevidencia, bool IncideIrrf);
    sealed record FaixaInss(int Ordem, decimal Min, decimal? Max, decimal Aliquota, decimal CoefRed);
    sealed record FaixaIrrf(decimal Min, decimal? Max, decimal Aliquota, decimal Deducao);
    sealed record VincCalc(long Id, long ServidorId, string Matricula, DateOnly Admissao);

    // ----------------------------- calculo individual (BR-12.2) ------------------------
    // Deterministico: INSS/RPPS por faixas parametricas (LC 51/1984 com coeficiente de
    // reducao na faixa de teto; RPPS faixa unica/plana), IRRF tabela progressiva mensal por
    // ano-base (Decreto 3.048/99), 13o por 1/12 de mes >= 15 dias nas parcelas parametricas
    // (situacao anual persistida em previdencia_decimo), teto constitucional por esfera.
    async Task<(decimal Bruto, decimal BasePrev, decimal Inss, decimal BaseIrrf, decimal Irrf, decimal Decimo, decimal Liquido, List<CalculoItemDto> Itens, int DecimoParcela)> CalcularVinculoAsync(
        NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, VincCalc v, DateOnly competencia, DateOnly dataFixa,
        string regime, decimal teto, string esfera, int mesDecimo1, int mesDecimo2, CancellationToken ct)
    {
        var itens = (await c.QueryAsync<(string Natureza, string Nome, decimal Valor, bool IncPrev, bool IncIrrf)>(new CommandDefinition(
            """
            select r.natureza_codigo, coalesce(n.nome, r.natureza_codigo), r.valor, coalesce(n.incide_previdencia,false), coalesce(n.incide_irrf,false)
            from sigov.previdencia_remun_item r
            left join sigov.previdencia_natureza n on n.codigo=r.natureza_codigo and n.ativo and not n.is_deleted
            where r.vinculo_id=@V and r.tenant_id=@T and r.ativo and not r.is_deleted
            """, new { V = v.Id, T = x.TenantId }, tx, cancellationToken: ct)))
            .Select(r => new ItemBase(r.Natureza, r.Nome, r.Valor, r.IncPrev, r.IncIrrf)).ToList();

        var vencimentoBase = itens.Where(i => i.IncidePrevidencia).Sum(i => i.Valor);

        // 13o salario (Lei 4.749/65 c/c Lei 12.735/2012): 1a parcela no mes parametrico (30/11),
        // 2a no mes parametrico (20/12); 1/12 por mes do ano com >= 15 dias trabalhados. A
        // situacao anual e persistida (upsert por tenant x vinculo x ano) — auditavel.
        var decimoValor = 0m;
        var decimoFormula = "";
        var decimoParcela = 0;
        var mes = competencia.Month;
        if (mes == mesDecimo1)
        {
            var n = MesesCom15Dias(v.Admissao, dataFixa, competencia.Year, mes);
            if (n > 0 && vencimentoBase > 0)
            {
                decimoValor = R2(vencimentoBase * n / 12m);
                decimoFormula = $"1/12 por mes >= 15 dias trabalhados (Lei 4.749/65): {n} meses completos";
                decimoParcela = 1;
                await c.ExecuteAsync(new CommandDefinition(
                    """
                    insert into sigov.previdencia_decimo(tenant_id,vinculo_id,ano,meses_ate_1a_parcela,parcela1_valor,status,created_by)
                    values (@T,@V,@A,@N,@P,'PARCELA1_REGISTRADA',@U)
                    on conflict (tenant_id,vinculo_id,ano) do update set
                      meses_ate_1a_parcela=excluded.meses_ate_1a_parcela, parcela1_valor=excluded.parcela1_valor,
                      status='PARCELA1_REGISTRADA', updated_at=now()
                    """, new { T = x.TenantId, V = v.Id, A = competencia.Year, N = n, P = decimoValor, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
        }
        else if (mes == mesDecimo2)
        {
            var row = await c.QueryFirstOrDefaultAsync<(int Meses, decimal P1)?>(new CommandDefinition(
                """
                select meses_ate_1a_parcela, parcela1_valor from sigov.previdencia_decimo
                where tenant_id=@T and vinculo_id=@V and ano=@A
                """, new { T = x.TenantId, V = v.Id, A = competencia.Year }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (row is null || row.Value.Meses <= 0 || row.Value.P1 <= 0) throw new DecimoSemPrimeiraParcelaException();
            var nFinal = MesesCom15Dias(v.Admissao, dataFixa, competencia.Year, mes);
            var totalAnual = R2(vencimentoBase * nFinal / 12m);
            decimoValor = Math.Max(0m, R2(totalAnual - row.Value.P1));
            decimoFormula = $"2a parcela = total anual - 1a parcela (Lei 4.749/65): {nFinal} meses no ano";
            decimoParcela = 2;
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.previdencia_decimo(tenant_id,vinculo_id,ano,meses_finais,parcela1_valor,parcela2_valor,status,created_by)
                values (@T,@V,@A,@NF,@P1,@P2,'ENCERRADO',@U)
                on conflict (tenant_id,vinculo_id,ano) do update set
                  meses_finais=excluded.meses_finais, parcela2_valor=excluded.parcela2_valor,
                  status='ENCERRADO', updated_at=now()
                """, new { T = x.TenantId, V = v.Id, A = competencia.Year, NF = nFinal, P1 = row.Value.P1, P2 = decimoValor, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }

        var todos = itens.ToList();
        if (decimoValor > 0) todos.Add(new ItemBase("DECIMO_SALARIO", "13o salário (Lei 4.749/65)", decimoValor, true, true));

        var bruto = todos.Sum(i => i.Valor);
        var basePrev = todos.Where(i => i.IncidePrevidencia).Sum(i => i.Valor);

        // INSS/RPPS (LC 51/1984 ou regime proprio parametrico) por regime + ano da competencia.
        var faixas = (await c.QueryAsync<(int Ordem, decimal Min, decimal Max, decimal? MaxN, decimal Aliq, decimal Coef)>(new CommandDefinition(
            """
            select ordem, limite_min, coalesce(limite_max, limite_min), limite_max, aliquota_pct, coeficiente_reducao
            from sigov.previdencia_inss_faixa where regime=@R and ano=@A order by ordem
            """, new { R = regime, A = competencia.Year }, tx, cancellationToken: ct)))
            .Select(f => new FaixaInss(f.Ordem, f.Min, f.MaxN, f.Aliq / 100m, f.Coef)).ToList();
        if (faixas.Count == 0) throw new PrevidenciaRecursoNaoDisponivelException($"previdencia_inss_faixa regime={regime} ano={competencia.Year}");
        var topo = faixas[faixas.Count - 1];
        var baseCalc = topo.Max is null ? basePrev : Math.Min(basePrev, topo.Max.Value);
        var inss = 0m;
        foreach (var f in faixas)
        {
            var sup = f.Max is null ? baseCalc : Math.Min(baseCalc, f.Max.Value);
            var porcao = Math.Max(0m, sup - f.Min);
            if (porcao > 0) inss += R2(porcao * f.Aliquota);
        }
        // parte acima do teto de contribuicao com coeficiente de reducao (estrutura LC 51/1984 art. 4 §2o)
        if (topo.Max is not null && basePrev > topo.Max.Value)
        {
            inss += R2((basePrev - topo.Max.Value) * topo.Aliquota);
            if (topo.CoefRed > 0) inss -= R2(topo.CoefRed * topo.Max.Value * topo.Aliquota);
        }
        inss = Math.Max(0m, R2(inss));

        // IRRF (Decreto 3.048/99): tabela progressiva mensal do ano da competencia sobre
        // (bruto - contribuicao previdenciaria) com deducao parametrica da faixa.
        var baseIrrf = bruto - inss;
        var faixasIr = (await c.QueryAsync<(decimal Min, decimal? Max, decimal Aliq, decimal Ded)>(new CommandDefinition(
            """
            select limite_min, limite_max, aliquota_pct, deducao
            from sigov.previdencia_irrf_faixa where ano_base=@A order by ordem
            """, new { A = competencia.Year }, tx, cancellationToken: ct)))
            .Select(f => new FaixaIrrf(f.Min, f.Max, f.Aliq / 100m, f.Ded)).ToList();
        if (faixasIr.Count == 0) throw new PrevidenciaRecursoNaoDisponivelException($"previdencia_irrf_faixa ano={competencia.Year}");
        var faixaIr = faixasIr.FirstOrDefault(f => baseIrrf >= f.Min && (f.Max is null || baseIrrf < f.Max.Value));
        if (faixaIr == null) throw new PrevidenciaRecursoNaoDisponivelException($"previdencia_irrf_faixa ano={competencia.Year} base={baseIrrf} fora das faixas");
        var irrf = Math.Max(0m, R2(baseIrrf * faixaIr.Aliquota) - faixaIr.Deducao);

        var liquido = R2(bruto - inss - irrf);

        // Teto constitucional por esfera (CF/88 art. 37 XV/XVI c/c EC 103/2019): bloqueia o fechamento.
        if (teto > 0 && bruto > teto) throw new ServidorAcimaDoTetoException(v.Matricula, bruto, teto, esfera);

        var itensDto = new List<CalculoItemDto>();
        var seq = 1;
        foreach (var i in itens)
            itensDto.Add(new CalculoItemDto(seq++, "RENDA", i.Nome, i.NaturezaCodigo, null, null, i.Valor, "item de remuneracao catalogado (natureza parametrizada)"));
        if (decimoValor > 0)
            itensDto.Add(new CalculoItemDto(seq++, "DECIMO_SALARIO", "13o salario - parcela " + decimoParcela, "DECIMO_SALARIO", null, null, decimoValor, decimoFormula));
        itensDto.Add(new CalculoItemDto(seq++, "PREVIDENCIA", $"Contribuição previdenciária ({regime})", null, basePrev, null, inss, $"faixas parametricas {regime} ano {competencia.Year} (LC 51/1984 / RPPS)"));
        itensDto.Add(new CalculoItemDto(seq++, "IRRF", "Imposto de renda fonte (tabela progressiva)", null, baseIrrf, null, irrf, $"tabela mensal ano {competencia.Year}: base*aliquota - deducao (Decreto 3.048/99)"));

        return (R2(bruto), R2(basePrev), inss, R2(baseIrrf), irrf, R2(decimoValor), liquido, itensDto, decimoParcela);
    }

    async Task<long> InsertCalculoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, long competenciaId, VincCalc v, DateOnly dataFixa, int versao,
        (decimal Bruto, decimal BasePrev, decimal Inss, decimal BaseIrrf, decimal Irrf, decimal Decimo, decimal Liquido, List<CalculoItemDto> Itens, int DecimoParcela) calc, CancellationToken ct)
    {
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_folha_calculo(tenant_id,competencia_id,vinculo_id,versao,data_base_calculo,bruto,base_previdencia,inss_valor,base_irrf,irrf_valor,decimo_valor,liquido,status,motivo_veto,created_by)
            values (@T,@C,@V,@Versao,@DF,@Bruto,@BP,@Inss,@BI,@Irrf,@Dec,@Liq,'VIGENTE',null,@U) returning id
            """, new { T = x.TenantId, C = competenciaId, V = v.Id, Versao = versao, DF = dataFixa, Bruto = calc.Bruto, BP = calc.BasePrev, Inss = calc.Inss, BI = calc.BaseIrrf, Irrf = calc.Irrf, Dec = calc.Decimo, Liq = calc.Liquido, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
        foreach (var it in calc.Itens)
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.previdencia_folha_calculo_item(tenant_id,calculo_id,sequencia,grupo,descricao,natureza_codigo,base,aliquota_pct,valor,formula,created_by)
                values (@T,@Calc,@Seq,@G,@D,@Nat,@B,null,@Va,@F,@U)
                """, new { T = x.TenantId, Calc = id, Seq = it.Sequencia, G = it.Grupo, D = it.Descricao, Nat = it.NaturezaCodigo, B = it.Base, Va = it.Valor, F = it.Formula, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return id;
    }

    // Rastreabilidade BR-12.7: o calculo que originou cada parcela do 13o fica auditavel.
    async Task BackfillDecimoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, PrevidenciaContext x, long vinculoId, int ano, int parcela, long calculoId, CancellationToken ct)
    {
        if (parcela is not (1 or 2)) return;
        var col = parcela == 1 ? "parcela1_calculo_id" : "parcela2_calculo_id";
        await c.ExecuteAsync(new CommandDefinition(
            $"update sigov.previdencia_decimo set {col}=@K, updated_at=now() where tenant_id=@T and vinculo_id=@V and ano=@A",
            new { K = calculoId, T = x.TenantId, V = vinculoId, A = ano }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    async Task<VincCalc> CarregarVinculoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, long vinculoId, long tenantId, CancellationToken ct)
    {
        var r = await c.QueryFirstOrDefaultAsync<(long Id, long ServidorId, string Matricula, DateOnly Admissao)>(new CommandDefinition(
            """
            select v.id, v.servidor_id, s.matricula_funcional, v.data_admissao
            from sigov.previdencia_vinculo v join sigov.previdencia_servidor s on s.id=v.servidor_id
            where v.id=@V and v.tenant_id=@T
            """, new { V = vinculoId, T = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (r.Id == 0) throw new VinculoInexistenteException();
        return new VincCalc(r.Id, r.ServidorId, r.Matricula, r.Admissao);
    }

    // ---------------------------------- CADASTRO (BR-12.1) -----------------------------
    public async Task<CargoDto> CriarCargoAsync(PrevidenciaContext x, CargoCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var modo = (r.ModoRemuneracao ?? "").Trim().ToUpperInvariant();
        if (modo is not ("SUBSIDIO" or "VENCIMENTO_ADICIONAIS")) throw new ArgumentException("MODO_REMUNERACAO_INVALIDO: use SUBSIDIO ou VENCIMENTO_ADICIONAIS.");
        if (string.IsNullOrWhiteSpace(r.CodigoExterno) || string.IsNullOrWhiteSpace(r.Nome)) throw new ArgumentException("CARGO_INCOMPLETO: codigo_externo e nome obrigatorios.");
        var existe = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.previdencia_cargo where tenant_id=@T and codigo_externo=@C", new { T = x.TenantId, C = r.CodigoExterno.Trim() }, cancellationToken: ct)).ConfigureAwait(false);
        if (existe > 0) throw new CargoJaExisteException();
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_cargo(tenant_id,codigo_externo,nome,classe,padrao,nivel,modo_remuneracao,valor_referencia,created_by)
            values (@T,@C,@N,@Cl,@P,@Ni,@M,@VR,@U) returning id
            """, new { T = x.TenantId, C = r.CodigoExterno.Trim(), N = r.Nome.Trim(), Cl = r.Classe, P = r.Padrao, Ni = r.Nivel, M = modo, VR = R2(r.ValorReferencia), U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        return new CargoDto(id, r.CodigoExterno.Trim(), r.Nome.Trim(), r.Classe, r.Padrao, r.Nivel, modo, R2(r.ValorReferencia), true);
    }

    public async Task<IReadOnlyList<CargoDto>> ListarCargosAsync(PrevidenciaContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var rows = await c.QueryAsync<(long Id, string Cod, string Nome, string? Classe, string? Padrao, string? Nivel, string Modo, decimal Vr, bool Ativo)>(new CommandDefinition(
            """
            select id, codigo_externo, nome, classe, padrao, nivel, modo_remuneracao, valor_referencia, ativo
            from sigov.previdencia_cargo where tenant_id=@T and not is_deleted order by codigo_externo
            """, new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new CargoDto(r.Id, r.Cod, r.Nome, r.Classe, r.Padrao, r.Nivel, r.Modo, r.Vr, r.Ativo)).ToList();
    }

    public async Task<ServidorDto> AdmitirServidorAsync(PrevidenciaContext x, ServidorAdmitirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.MatriculaFuncional) || string.IsNullOrWhiteSpace(r.Nome))
            throw new ArgumentException("SERVIDOR_INCOMPLETO: matricula_funcional e nome obrigatorios.");
        var tipo = (r.TipoVinculo ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("EFETIVO" or "COMISSAO" or "TEMPORARIO")) throw new ArgumentException("VINCULO_TIPO_INVALIDO: use EFETIVO, COMISSAO ou TEMPORARIO.");
        using var c = factory.CreateConnection();
        var cargo = await c.QueryFirstOrDefaultAsync<(long Id, string Codigo, string Nome, decimal Vr)>(new CommandDefinition(
            "select id, codigo_externo, nome, valor_referencia from sigov.previdencia_cargo where tenant_id=@T and codigo_externo=@C and ativo and not is_deleted",
            new { T = x.TenantId, C = r.CargoCodigo.Trim() }, cancellationToken: ct)).ConfigureAwait(false);
        if (cargo.Id == 0) throw new CargoInexistenteException();
        var dup = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.previdencia_servidor where tenant_id=@T and matricula_funcional=@M", new { T = x.TenantId, M = r.MatriculaFuncional.Trim() }, cancellationToken: ct)).ConfigureAwait(false);
        if (dup > 0) throw new MatriculaFuncionalDuplicadaException();
        var esfera = await EsferaAsync(c, null, x, ct).ConfigureAwait(false);
        var servId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_servidor(tenant_id,entidade_id,matricula_funcional,nome,esfera_governo,uf,data_cadastro,created_by)
            values (@T,@E,@M,@N,@Es,@Uf,current_date,@U) returning id
            """, new { T = x.TenantId, E = x.EntidadeId ?? throw new EntidadeSemEsferaException(), M = r.MatriculaFuncional.Trim(), N = r.Nome.Trim(), Es = esfera, Uf = r.Uf, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        var vincId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_vinculo(tenant_id,servidor_id,cargo_id,tipo_vinculo,data_admissao,situacao,created_by)
            values (@T,@S,@C,@Tipo,@Ad,'ATIVO',@U) returning id
            """, new { T = x.TenantId, S = servId, C = cargo.Id, Tipo = tipo, Ad = r.DataAdmissao, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.previdencia_remun_item(tenant_id,vinculo_id,natureza_codigo,valor,created_by)
            values (@T,@V,'VENCIMENTO',@Vr,@U) on conflict (vinculo_id,natureza_codigo) do nothing
            """, new { T = x.TenantId, V = vincId, Vr = cargo.Vr, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        return new ServidorDto(servId, r.MatriculaFuncional.Trim(), r.Nome.Trim(), esfera, r.Uf, vincId, "ATIVO", tipo, r.DataAdmissao, cargo.Codigo, cargo.Nome, cargo.Vr);
    }

    public async Task<IReadOnlyList<ServidorDto>> ListarServidoresAsync(PrevidenciaContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var rows = await c.QueryAsync<(long Id, string Mat, string Nome, string Esfera, string? Uf, long Vinc, string Sit, string Tipo, DateOnly Ad, string CgCod, string CgNome, decimal Base)>(new CommandDefinition(
            """
            select s.id, s.matricula_funcional, s.nome, s.esfera_governo, s.uf, v.id, v.situacao, v.tipo_vinculo, v.data_admissao,
                   cg.codigo_externo, cg.nome, coalesce(b.base,0)
            from sigov.previdencia_servidor s
            join sigov.previdencia_vinculo v on v.servidor_id=s.id and v.tenant_id=s.tenant_id
            join sigov.previdencia_cargo cg on cg.id=v.cargo_id
            left join lateral (select sum(r.valor) as base from sigov.previdencia_remun_item r
                                join sigov.previdencia_natureza n on n.codigo=r.natureza_codigo
                               where r.vinculo_id=v.id and n.incide_previdencia and r.ativo and not r.is_deleted) b on true
            where s.tenant_id=@T and not s.is_deleted order by s.matricula_funcional
            """, new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new ServidorDto(r.Id, r.Mat, r.Nome, r.Esfera, r.Uf, r.Vinc, r.Sit, r.Tipo, r.Ad, r.CgCod, r.CgNome, r.Base)).ToList();
    }

    public async Task<RemunItemDto> RegistrarRemuneracaoAsync(PrevidenciaContext x, RemuneracaoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Valor < 0) throw new ArgumentException("VALOR_REMUNERACAO_NEGATIVO: valor nao pode ser negativo.");
        using var c = factory.CreateConnection();
        _ = await CarregarVinculoAsync(c, null, r.VinculoId, x.TenantId, ct).ConfigureAwait(false);
        var nat = await c.QueryFirstOrDefaultAsync<(string Cod, string Nome, bool IncP, bool IncI)>(new CommandDefinition(
            "select codigo, nome, incide_previdencia, incide_irrf from sigov.previdencia_natureza where codigo=@N and ativo and not is_deleted",
            new { N = r.NaturezaCodigo.Trim() }, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(nat.Cod)) throw new NaturezaRemuneracaoInexistenteException(r.NaturezaCodigo);
        var atual = await c.QueryFirstOrDefaultAsync<(long Id, decimal Valor)>(new CommandDefinition(
            "select id, valor from sigov.previdencia_remun_item where vinculo_id=@V and natureza_codigo=@N and not is_deleted",
            new { V = r.VinculoId, N = r.NaturezaCodigo.Trim() }, cancellationToken: ct)).ConfigureAwait(false);
        long id;
        if (atual.Id != 0)
        {
            if (!r.Alterar) throw new RemuneracaoJaExisteException();
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.previdencia_remun_item set valor=@Va, updated_at=now(), updated_by=@U where id=@Id",
                new { Va = R2(r.Valor), U = U(x), Id = atual.Id }, cancellationToken: ct)).ConfigureAwait(false);
            id = atual.Id;
        }
        else
        {
            id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                "insert into sigov.previdencia_remun_item(tenant_id,vinculo_id,natureza_codigo,valor,created_by) values (@T,@V,@N,@Va,@U) returning id",
                new { T = x.TenantId, V = r.VinculoId, N = r.NaturezaCodigo.Trim(), Va = R2(r.Valor), U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        }
        return new RemunItemDto(id, r.VinculoId, r.NaturezaCodigo.Trim(), R2(r.Valor), nat.IncP, nat.IncI);
    }

    public async Task<long> DesligarServidorAsync(PrevidenciaContext x, DesligamentoRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Motivo)) throw new ArgumentException("DESLIGAMENTO_SEM_MOTIVO: desligamento exige motivo registrado.");
        using var c = factory.CreateConnection();
        var v = await c.QueryFirstOrDefaultAsync<(long Id, string Sit)>(new CommandDefinition(
            "select id, situacao from sigov.previdencia_vinculo where id=@V and tenant_id=@T", new { V = r.VinculoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (v.Id == 0) throw new VinculoInexistenteException();
        if (v.Sit != "ATIVO") throw new VinculoNaoAtivoException();
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.previdencia_vinculo set situacao='DESLIGADO', data_desligamento=@D, motivo_desligamento=@M, updated_at=now(), updated_by=@U where id=@V",
            new { D = r.DataDesligamento, M = r.Motivo, U = U(x), V = r.VinculoId }, cancellationToken: ct)).ConfigureAwait(false);
        return r.VinculoId;
    }

    // ------------------------------- FOLHA (BR-12.2/12.5/12.6) -------------------------
    static string SqlTotaisCompetencia =>
        """
        select count(1), coalesce(sum(bruto),0), coalesce(sum(base_previdencia),0), coalesce(sum(inss_valor),0),
               coalesce(sum(base_irrf),0), coalesce(sum(irrf_valor),0), coalesce(sum(decimo_valor),0), coalesce(sum(liquido),0)
        from sigov.previdencia_folha_calculo where competencia_id=@C and status='VIGENTE'
        """;

    public async Task<FolhaCompetenciaDto> AbrirCompetenciaAsync(PrevidenciaContext x, FolhaCompetenciaRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Competencia.Day != 1) throw new CompetenciaInvalidaException();
        if (r.DataFixa < r.Competencia) throw new DataFixaInvalidaException();
        using var c = factory.CreateConnection();
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.previdencia_folha_competencia(tenant_id,competencia,status,created_by)
            values (@T,@C,'ABERTA',@U)
            on conflict (tenant_id,competencia) do nothing
            """, new { T = x.TenantId, C = r.Competencia, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        var row = await c.QueryFirstOrDefaultAsync<(long Id, string Status, DateTime? FechadaEm, string? Just, int N)>(new CommandDefinition(
            """
            select fc.id, fc.status, fc.fechada_em, fc.justificativa_reabertura,
                   (select count(1) from sigov.previdencia_folha_calculo k where k.competencia_id=fc.id and k.status='VIGENTE')
            from sigov.previdencia_folha_competencia fc where fc.tenant_id=@T and fc.competencia=@C
            """, new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (row.Id == 0) throw new FolhaCompetenciaInexistenteException();
        return new FolhaCompetenciaDto(row.Id, r.Competencia, row.Status, row.FechadaEm, row.Just, row.N);
    }

    public async Task<FolhaFechamentoDto> FecharFolhaAsync(PrevidenciaContext x, FolhaCompetenciaRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Competencia.Day != 1) throw new CompetenciaInvalidaException();
        if (r.DataFixa < r.Competencia) throw new DataFixaInvalidaException();
        using var c = factory.CreateConnection();
        var comp = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.previdencia_folha_competencia where tenant_id=@T and competencia=@C",
            new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (comp.Id == 0) throw new FolhaCompetenciaInexistenteException();
        var compId = comp.Id;

        async Task<(int N, CalculoResumo Tot)> TotaisAsync(long cid, CancellationToken c2)
        {
            var t = await c.QueryFirstAsync<(int N, decimal Bruto, decimal Bp, decimal Inss, decimal Bi, decimal Irrf, decimal Dec, decimal Liq)>(new CommandDefinition(
                SqlTotaisCompetencia, new { C = cid }, cancellationToken: c2)).ConfigureAwait(false);
            return (t.N, new CalculoResumo(t.Bruto, t.Bp, t.Inss, t.Bi, t.Irrf, t.Dec, t.Liq));
        }

        // BR-12.2 idempotente: fechar de novo uma competencia FECHADA devolve o estado consolidado.
        if (comp.Status == "FECHADA")
        {
            var t = await TotaisAsync(compId, ct).ConfigureAwait(false);
            return new FolhaFechamentoDto(compId, r.Competencia, "FECHADA", true, 0, t.N, t.Tot);
        }

        var esfera = await EsferaAsync(c, null, x, ct).ConfigureAwait(false);
        var regime = await ParamTextoAsync(c, null, x, "PREVIDENCIA_REGIME", ct).ConfigureAwait(false);
        var teto = await ParamDecimalAsync(c, null, x, "TETO_REMUNERACAO_" + esfera.ToUpperInvariant(), ct).ConfigureAwait(false);
        var mes1 = await ParamIntAsync(c, null, x, "DECIMO_1A_PARCELA_MES", ct).ConfigureAwait(false);
        var mes2 = await ParamIntAsync(c, null, x, "DECIMO_2A_PARCELA_MES", ct).ConfigureAwait(false);

        var vinculos = (await c.QueryAsync<(long Id, long ServidorId, string Matricula, DateOnly Admissao)>(new CommandDefinition(
            """
            select v.id, v.servidor_id, s.matricula_funcional, v.data_admissao
            from sigov.previdencia_vinculo v join sigov.previdencia_servidor s on s.id=v.servidor_id
            where v.tenant_id=@T and v.situacao='ATIVO' and not v.is_deleted and v.data_admissao <= @DF
            order by v.id
            """, new { T = x.TenantId, DF = r.DataFixa }, cancellationToken: ct)))
            .Select(t => new VincCalc(t.Id, t.ServidorId, t.Matricula, t.Admissao)).ToList();

        var gerados = 0;
        using (var tx = c.BeginTransaction())
        {
            foreach (var v in vinculos)
            {
                if (await c.ExecuteScalarAsync<int>(new CommandDefinition(
                        "select count(1) from sigov.previdencia_folha_calculo where tenant_id=@T and vinculo_id=@V and competencia_id=@C and status='VIGENTE'",
                        new { T = x.TenantId, V = v.Id, C = compId }, tx, cancellationToken: ct)).ConfigureAwait(false) > 0)
                    continue;
                var calc = await CalcularVinculoAsync(c, tx, x, v, r.Competencia, r.DataFixa, regime, teto, esfera, mes1, mes2, ct).ConfigureAwait(false);
                var calcId = await InsertCalculoAsync(c, tx, x, compId, v, r.DataFixa, 1, calc, ct).ConfigureAwait(false);
                await BackfillDecimoAsync(c, tx, x, v.Id, r.Competencia.Year, calc.DecimoParcela, calcId, ct).ConfigureAwait(false);
                gerados++;
            }
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.previdencia_folha_competencia set status='FECHADA', fechada_em=now(), usuario_fechou=@U, updated_at=now() where id=@C and tenant_id=@T",
                new { U = U(x), C = compId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            tx.Commit();
        }
        var tot = await TotaisAsync(compId, ct).ConfigureAwait(false);
        return new FolhaFechamentoDto(compId, r.Competencia, "FECHADA", false, gerados, tot.N, tot.Tot);
    }

    public async Task<FolhaCompetenciaDto> ReabrirFolhaAsync(PrevidenciaContext x, FolhaReabrirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Competencia.Day != 1) throw new CompetenciaInvalidaException();
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new ReaberturaSemJustificativaException();
        using var c = factory.CreateConnection();
        var comp = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.previdencia_folha_competencia where tenant_id=@T and competencia=@C",
            new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (comp.Id == 0) throw new FolhaCompetenciaInexistenteException();
        if (comp.Status != "FECHADA") throw new FolhaNaoEstavaFechadaException();
        await c.ExecuteAsync(new CommandDefinition(
            """
            update sigov.previdencia_folha_competencia
               set status='ABERTA', fechada_em=null, reaberta_em=now(), justificativa_reabertura=@J, updated_at=now(), updated_by=@U
             where id=@C and tenant_id=@T
            """, new { J = r.Justificativa.Trim(), U = U(x), C = comp.Id, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        var row = await c.QueryFirstAsync<(long Id, string Status, DateTime? FechadaEm, string? Just, int N)>(new CommandDefinition(
            """
            select id, status, fechada_em, justificativa_reabertura,
                   (select count(1) from sigov.previdencia_folha_calculo k where k.competencia_id=fc.id and k.status='VIGENTE')
            from sigov.previdencia_folha_competencia fc where fc.tenant_id=@T and fc.competencia=@C
            """, new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        return new FolhaCompetenciaDto(row.Id, r.Competencia, row.Status, row.FechadaEm, row.Just, row.N);
    }

    // ---------------------------- REPROCESSO (BR-12.3) ---------------------------------
    // Nova versao que VETA a anterior (ambas permanecem auditaveis - backstop 1 VIGENTE).
    public async Task<CalculoDto> ReprocessarCalculoAsync(PrevidenciaContext x, ReprocessarCalculoRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Competencia.Day != 1) throw new CompetenciaInvalidaException();
        if (r.DataFixa < r.Competencia) throw new DataFixaInvalidaException();
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new ReprocessoSemJustificativaException();
        using var c = factory.CreateConnection();
        var comp = await c.QueryFirstOrDefaultAsync<long>(new CommandDefinition(
            "select id from sigov.previdencia_folha_competencia where tenant_id=@T and competencia=@C",
            new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (comp == 0) throw new FolhaCompetenciaInexistenteException();
        var vigente = await c.QueryFirstOrDefaultAsync<(long Id, int Versao)>(new CommandDefinition(
            "select id, versao from sigov.previdencia_folha_calculo where tenant_id=@T and vinculo_id=@V and competencia_id=@C and status='VIGENTE'",
            new { T = x.TenantId, V = r.VinculoId, C = comp }, cancellationToken: ct)).ConfigureAwait(false);
        if (vigente.Id == 0) throw new CalculoVigenteInexistenteException();
        var v = await CarregarVinculoAsync(c, null, r.VinculoId, x.TenantId, ct).ConfigureAwait(false);
        var esfera = await EsferaAsync(c, null, x, ct).ConfigureAwait(false);
        var regime = await ParamTextoAsync(c, null, x, "PREVIDENCIA_REGIME", ct).ConfigureAwait(false);
        var teto = await ParamDecimalAsync(c, null, x, "TETO_REMUNERACAO_" + esfera.ToUpperInvariant(), ct).ConfigureAwait(false);
        var mes1 = await ParamIntAsync(c, null, x, "DECIMO_1A_PARCELA_MES", ct).ConfigureAwait(false);
        var mes2 = await ParamIntAsync(c, null, x, "DECIMO_2A_PARCELA_MES", ct).ConfigureAwait(false);
        long novoId;
        using (var tx = c.BeginTransaction())
        {
            var calc = await CalcularVinculoAsync(c, tx, x, v, r.Competencia, r.DataFixa, regime, teto, esfera, mes1, mes2, ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.previdencia_folha_calculo set status='VETADA', motivo_veto=@J, vetada_em=now(), updated_by=@U where id=@Id and status='VIGENTE'",
                new { J = r.Justificativa.Trim(), U = U(x), Id = vigente.Id }, tx, cancellationToken: ct)).ConfigureAwait(false);
            novoId = await InsertCalculoAsync(c, tx, x, comp, v, r.DataFixa, vigente.Versao + 1, calc, ct).ConfigureAwait(false);
            await BackfillDecimoAsync(c, tx, x, v.Id, r.Competencia.Year, calc.DecimoParcela, novoId, ct).ConfigureAwait(false);
        }
        return await MontarCalculoAsync(c, x, novoId, r.VinculoId, r.Competencia, ct).ConfigureAwait(false);
    }

    async Task<List<CalculoItemDto>> ItensCalculoAsync(NpgsqlConnection c, long calculoId, CancellationToken ct)
    {
        var rows = await c.QueryAsync<(int Seq, string Grupo, string Desc, string? Nat, decimal? Base, decimal Valor, string? F)>(new CommandDefinition(
            """
            select sequencia, grupo, descricao, natureza_codigo, base, valor, formula
            from sigov.previdencia_folha_calculo_item where calculo_id=@C order by sequencia
            """, new { C = calculoId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new CalculoItemDto(r.Seq, r.Grupo, r.Desc, r.Nat, r.Base, null, r.Valor, r.F)).ToList();
    }

    async Task<CalculoDto> MontarCalculoAsync(NpgsqlConnection c, PrevidenciaContext x, long calculoId, long vinculoId, DateOnly competencia, CancellationToken ct)
    {
        var row = await c.QueryFirstAsync<(long Id, int Versao, DateOnly Df, string Status, string? Motivo, decimal Bruto, decimal Bp, decimal Inss, decimal Bi, decimal Irrf, decimal Dec, decimal Liq)>(new CommandDefinition(
            """
            select id, versao, data_base_calculo, status, motivo_veto, bruto, base_previdencia, inss_valor, base_irrf, irrf_valor, decimo_valor, liquido
            from sigov.previdencia_folha_calculo where id=@C and tenant_id=@T
            """, new { C = calculoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return new CalculoDto(row.Id, vinculoId, competencia, row.Versao, row.Df, row.Status, row.Motivo,
            new CalculoResumo(row.Bruto, row.Bp, row.Inss, row.Bi, row.Irrf, row.Dec, row.Liq),
            await ItensCalculoAsync(c, calculoId, ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<CalculoVersaoDto>> ListarCalculosAsync(PrevidenciaContext x, long vinculoId, DateOnly competencia, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var rows = await c.QueryAsync<(long Id, int Versao, string Status, DateOnly Df, string? Motivo, DateTime? VetadaEm)>(new CommandDefinition(
            """
            select k.id, k.versao, k.status, k.data_base_calculo, k.motivo_veto, k.vetada_em
            from sigov.previdencia_folha_calculo k
            join sigov.previdencia_folha_competencia f on f.id=k.competencia_id
            where k.tenant_id=@T and k.vinculo_id=@V and f.competencia=@C
            order by k.versao desc
            """, new { T = x.TenantId, V = vinculoId, C = competencia }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new CalculoVersaoDto(r.Id, r.Versao, r.Status, r.Df, r.Motivo, r.VetadaEm)).ToList();
    }

    // --------------------------- CONTRACHEQUE (BR-12.7) -------------------------------
    // LGPD: dados minimos (matricula funcional identifica; nenhum CPF armazenado nesta vertical).
    public async Task<ContrachequeDto> ObterContrachequeAsync(PrevidenciaContext x, long vinculoId, DateOnly competencia, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var row = await c.QueryFirstOrDefaultAsync<(long KId, int Versao, DateOnly Df, string Status, string? Motivo, decimal Bruto, decimal Bp, decimal Inss, decimal Bi, decimal Irrf, decimal Dec, decimal Liq,
            string Mat, string Nome, string Esfera, string CgCod, string CgNome, string? Classe)>(new CommandDefinition(
            """
            select k.id, k.versao, k.data_base_calculo, k.status, k.motivo_veto, k.bruto, k.base_previdencia, k.inss_valor, k.base_irrf, k.irrf_valor, k.decimo_valor, k.liquido,
                   s.matricula_funcional, s.nome, s.esfera_governo, g.codigo_externo, g.nome, g.classe
            from sigov.previdencia_folha_calculo k
            join sigov.previdencia_vinculo v on v.id=k.vinculo_id
            join sigov.previdencia_servidor s on s.id=v.servidor_id
            join sigov.previdencia_cargo g on g.id=v.cargo_id
            join sigov.previdencia_folha_competencia f on f.id=k.competencia_id
            where k.tenant_id=@T and k.vinculo_id=@V and f.competencia=@C and k.status='VIGENTE'
            """, new { T = x.TenantId, V = vinculoId, C = competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (row.KId == 0) throw new CalculoVigenteInexistenteException();
        return new ContrachequeDto(row.Mat, row.Nome, row.Esfera, row.CgCod, row.CgNome, row.Classe, competencia, row.Versao, row.Df, row.Status,
            row.Bruto, row.Bp, row.Inss, row.Bi, row.Irrf, row.Dec, row.Liq, await ItensCalculoAsync(c, row.KId, ct).ConfigureAwait(false));
    }

    // --------------------------- FERIAS (BR-12.4) --------------------------------------
    public async Task<FeriasApuracaoDto> ApurarAquisicaoFeriasAsync(PrevidenciaContext x, FeriasVinculoRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var v = await CarregarVinculoAsync(c, null, r.VinculoId, x.TenantId, ct).ConfigureAwait(false);
        var diasCiclo = await ParamIntAsync(c, null, x, "FERIAS_DIAS_POR_CICLO", ct).ConfigureAwait(false);
        var existentes = (await c.QueryAsync<DateOnly>(new CommandDefinition(
            "select inicio_aquisitivo from sigov.previdencia_ferias where tenant_id=@T and vinculo_id=@V",
            new { T = x.TenantId, V = v.Id }, cancellationToken: ct))).ToHashSet();
        var criados = 0;
        // ciclo aquisitivo: cada periodo de 12 meses concluido ate a data base
        // (CLT art. 130; Lei 8.112/90 art. 82) - idempotente por inicio_aquisitivo.
        for (var k = 0; ; k++)
        {
            var inicio = v.Admissao.AddMonths(k * 12);
            var fim = inicio.AddMonths(12).AddDays(-1);
            if (fim > r.DataFixa) break;
            if (!existentes.Contains(inicio))
            {
                await c.ExecuteAsync(new CommandDefinition(
                    """
                    insert into sigov.previdencia_ferias(tenant_id,vinculo_id,inicio_aquisitivo,fim_aquisitivo,dias_adquiridos,saldo_dias,created_by)
                    values (@T,@V,@I,@F,@D,@D,@U)
                    on conflict (tenant_id,vinculo_id,inicio_aquisitivo) do nothing
                    """, new { T = x.TenantId, V = v.Id, I = inicio, F = fim, D = diasCiclo, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
                criados++;
            }
        }
        if (criados == 0) throw new SemCicloAquisitivoNovoException();
        var ciclos = (await c.QueryAsync<(long Id, DateOnly I, DateOnly F, int D, int G, int Ab, int S)>(new CommandDefinition(
            "select id, inicio_aquisitivo, fim_aquisitivo, dias_adquiridos, dias_gozados, dias_abonados, saldo_dias from sigov.previdencia_ferias where tenant_id=@T and vinculo_id=@V order by inicio_aquisitivo",
            new { T = x.TenantId, V = v.Id }, cancellationToken: ct)))
            .Select(r2 => new FeriasCicloDto(r2.Id, r2.I, r2.F, r2.D, r2.G, r2.Ab, r2.S)).ToList();
        return new FeriasApuracaoDto(v.Id, criados, ciclos.Count, ciclos.Sum(z => z.SaldoDias), ciclos);
    }

    // Ordem das validacoes (deterministica): periodo -> aviso previo -> fracao minima ->
    // abono minimo -> saldo -> fracionamento maximo. Todas nomeadas (regra 13).
    public async Task<FeriasProgramacaoDto> ProgramarFeriasAsync(PrevidenciaContext x, FeriasProgramarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.DataFim < r.DataInicio) throw new PeriodoInvalidoException();
        if (r.DiasGoze <= 0) throw new ArgumentException("DIAS_GOZE_INVALIDO: dias de gozo devem ser positivos.");
        if (r.DiasAbono < 0) throw new ArgumentException("DIAS_ABONO_INVALIDO: dias de abono nao podem ser negativos.");
        using var c = factory.CreateConnection();
        var v = await CarregarVinculoAsync(c, null, r.VinculoId, x.TenantId, ct).ConfigureAwait(false);
        var sit = await c.ExecuteScalarAsync<string>(new CommandDefinition(
            "select situacao from sigov.previdencia_vinculo where id=@V", new { V = v.Id }, cancellationToken: ct)).ConfigureAwait(false);
        if (sit != "ATIVO") throw new VinculoNaoAtivoException();
        var avisoMin = await ParamIntAsync(c, null, x, "FERIAS_INTERVALO_MINIMO_DIAS", ct).ConfigureAwait(false);
        var fracMin = await ParamIntAsync(c, null, x, "FERIAS_FRAC_MINIMO_DIAS", ct).ConfigureAwait(false);
        var abonoMin = await ParamIntAsync(c, null, x, "FERIAS_ACONTO_MINIMO_DIAS", ct).ConfigureAwait(false);
        var fracMax = await ParamIntAsync(c, null, x, "FERIAS_FRACIONAMENTO_MAXIMO", ct).ConfigureAwait(false);
        var aviso = r.DataInicio.DayNumber - r.DataFixa.DayNumber;
        if (aviso < avisoMin) throw new IntervaloFeriasInsuficienteException(aviso, avisoMin);
        if (r.DiasGoze < fracMin) throw new FracaoFeriasInsuficienteException(r.DiasGoze, fracMin);
        if (r.DiasAbono > 0 && r.DiasAbono < abonoMin) throw new AbonoPecuniarioMinimoException(r.DiasAbono, abonoMin);
        var f = await c.QueryFirstOrDefaultAsync<(long Id, int Saldo)>(new CommandDefinition(
            """
            select id, saldo_dias from sigov.previdencia_ferias
            where tenant_id=@T and vinculo_id=@V and saldo_dias>0 and fim_aquisitivo<=@DF
            order by inicio_aquisitivo desc limit 1
            """, new { T = x.TenantId, V = v.Id, DF = r.DataFixa }, cancellationToken: ct)).ConfigureAwait(false);
        if (f.Id == 0) throw new FeriasCicloInexistenteException();
        if (r.DiasGoze + r.DiasAbono > f.Saldo) throw new SaldoFeriasInsuficienteException(r.DiasGoze + r.DiasAbono, f.Saldo);
        var periodos = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.previdencia_ferias_programacao where ferias_id=@F and status in ('PROGRAMADA','GOZO')",
            new { F = f.Id }, cancellationToken: ct)).ConfigureAwait(false);
        if (periodos + 1 > fracMax) throw new FracionamentoFeriasExcedidoException(periodos + 1, fracMax);
        var baseSalario = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(r.valor),0) from sigov.previdencia_remun_item r
            join sigov.previdencia_natureza n on n.codigo=r.natureza_codigo
            where r.vinculo_id=@V and n.incide_previdencia and r.ativo and not r.is_deleted
            """, new { V = v.Id }, cancellationToken: ct)).ConfigureAwait(false);
        // adicional constitucional de 1/3 (Lei 8.112/90 art. 76; CLT art. 145) auditavel:
        // salario_dia = base/30; terco = salario_dia x (goze + abono) / 3.
        var salarioDia = R2(baseSalario / 30m);
        var terco = R2(salarioDia * (r.DiasGoze + r.DiasAbono) / 3m);
        var progId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_ferias_programacao(tenant_id,ferias_id,data_inicio,data_fim,dias_goze,dias_abono,salario_dia,adicional_terco,status,created_by)
            values (@T,@F,@I,@Fim,@G,@Ab,@SD,@Tc,'PROGRAMADA',@U) returning id
            """, new { T = x.TenantId, F = f.Id, I = r.DataInicio, Fim = r.DataFim, G = r.DiasGoze, Ab = r.DiasAbono, SD = salarioDia, Tc = terco, U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            update sigov.previdencia_ferias set saldo_dias=saldo_dias-(@G+@Ab), dias_gozados=dias_gozados+@G, dias_abonados=dias_abonados+@Ab, updated_at=now(), updated_by=@U
            where id=@F
            """, new { G = r.DiasGoze, Ab = r.DiasAbono, U = U(x), F = f.Id }, cancellationToken: ct)).ConfigureAwait(false);
        return new FeriasProgramacaoDto(progId, f.Id, r.DataInicio, r.DataFim, r.DiasGoze, r.DiasAbono, salarioDia, terco, "PROGRAMADA", f.Saldo - r.DiasGoze - r.DiasAbono);
    }

    public async Task<long> RegistrarGozoFeriasAsync(PrevidenciaContext x, FeriasGozoRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var p = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.previdencia_ferias_programacao where id=@P and tenant_id=@T",
            new { P = r.ProgramacaoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (p.Id == 0) throw new FeriasProgramacaoNaoProgramadaException();
        if (p.Status != "PROGRAMADA") throw new FeriasProgramacaoNaoProgramadaException();
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.previdencia_ferias_programacao set status='GOZO', gozo_em=now(), updated_at=now() where id=@P",
            new { P = r.ProgramacaoId }, cancellationToken: ct)).ConfigureAwait(false);
        return r.ProgramacaoId;
    }

    // ------------------------ AFASTAMENTOS (BR-12.4) -----------------------------------
    public async Task<AfastamentoDto> RegistrarAfastamentoAsync(PrevidenciaContext x, AfastamentoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        var tipo = (r.Tipo ?? "").Trim().ToUpperInvariant();
        if (tipo is not ("SAUDE" or "ESTUDO" or "MATERNIDADE" or "PATERNIDADE" or "ADOCAO")) throw new AfastamentoTipoInvalidoException();
        if (string.IsNullOrWhiteSpace(r.DocumentoGed)) throw new DocumentoGedObrigatoriaException();
        if (r.DataFim < r.DataInicio) throw new PeriodoInvalidoException();
        var regimeOpcao = (r.RegimeOpcao ?? "RPSS").Trim().ToUpperInvariant();
        if (regimeOpcao is not ("RGPS" or "RPSS")) throw new ArgumentException("REGIME_OPCAO_INVALIDO: use RGPS ou RPSS.");
        using var c = factory.CreateConnection();
        _ = await CarregarVinculoAsync(c, null, r.VinculoId, x.TenantId, ct).ConfigureAwait(false);
        var realDias = r.DataFim.DayNumber - r.DataInicio.DayNumber + 1;
        var tipoFixo = tipo switch
        {
            "MATERNIDADE" => "AFASTAMENTO_MATERNIDADE_DIAS",
            "PATERNIDADE" => "AFASTAMENTO_PATERNIDADE_DIAS",
            "ADOCAO" => "AFASTAMENTO_ADOCAO_DIAS",
            _ => null
        };
        if (tipoFixo is not null)
        {
            var esperado = await ParamIntAsync(c, null, x, tipoFixo, ct).ConfigureAwait(false);
            if (realDias != esperado) throw new AfastamentoDuracaoInconsistenteException(tipo, esperado, realDias);
        }
        var sobreposto = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.previdencia_afastamento where tenant_id=@T and vinculo_id=@V and status='ATIVO' and data_inicio<=@F and data_fim>=@I",
            new { T = x.TenantId, V = r.VinculoId, F = r.DataFim, I = r.DataInicio }, cancellationToken: ct)).ConfigureAwait(false);
        if (sobreposto > 0) throw new AfastamentoSobrepostoException();
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.previdencia_afastamento(tenant_id,vinculo_id,tipo,regime_opcao,data_inicio,data_fim,documento_ged,status,created_by)
            values (@T,@V,@Tipo,@Ro,@I,@F,@Ged,'ATIVO',@U) returning id
            """, new { T = x.TenantId, V = r.VinculoId, Tipo = tipo, Ro = regimeOpcao, I = r.DataInicio, F = r.DataFim, Ged = r.DocumentoGed.Trim(), U = U(x) }, cancellationToken: ct)).ConfigureAwait(false);
        return new AfastamentoDto(id, r.VinculoId, tipo, regimeOpcao, r.DataInicio, r.DataFim, realDias, r.DocumentoGed.Trim(), "ATIVO", null, null);
    }

    // Devolucao condicional: so e liberada quando a data base atinge/ultrapassa o fim do
    // afastamento (condicao de devolucao automatica avaliada no banco, sem manipulacao manual).
    public async Task<AfastamentoDto> DevolverAfastamentoAsync(PrevidenciaContext x, AfastamentoDevolverRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Motivo)) throw new ArgumentException("DEVOLUCAO_SEM_MOTIVO: devolucao exige motivo registrado.");
        using var c = factory.CreateConnection();
        var a = await c.QueryFirstOrDefaultAsync<(long Id, long V, string Tipo, string Ro, DateOnly I, DateOnly F, string Ged, string St, string? M)>(new CommandDefinition(
            "select id, vinculo_id, tipo, regime_opcao, data_inicio, data_fim, documento_ged, status, motivo_devolucao from sigov.previdencia_afastamento where id=@A and tenant_id=@T",
            new { A = r.AfastamentoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (a.Id == 0) throw new AfastamentoInexistenteException();
        if (a.St != "ATIVO") throw new AfastamentoJaDevolvidoException();
        if (r.DataFixa < a.F) throw new DevolucaoAfastamentoAntecipadaException();
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.previdencia_afastamento set status='DEVOLVIDO', devolvido_em=now(), motivo_devolucao=@M, updated_at=now() where id=@A",
            new { M = r.Motivo.Trim(), A = r.AfastamentoId }, cancellationToken: ct)).ConfigureAwait(false);
        return new AfastamentoDto(a.Id, a.V, a.Tipo, a.Ro, a.I, a.F, a.F.DayNumber - a.I.DayNumber + 1, a.Ged, "DEVOLVIDO", clock.UtcNow.UtcDateTime, r.Motivo.Trim());
    }

    // ------------------------------ DECIMO SALARIO -------------------------------------
    public async Task<DecimoSituacaoDto> SituacaoDecimoAsync(PrevidenciaContext x, long vinculoId, int ano, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var r = await c.QueryFirstOrDefaultAsync<(string St, int? M1, int? MF, decimal? P1, decimal? P2)>(new CommandDefinition(
            "select status, meses_ate_1a_parcela, meses_finais, parcela1_valor, parcela2_valor from sigov.previdencia_decimo where tenant_id=@T and vinculo_id=@V and ano=@A",
            new { T = x.TenantId, V = vinculoId, A = ano }, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(r.St)) return new DecimoSituacaoDto(vinculoId, ano, "EM_ACUMULO", null, null, null, null);
        return new DecimoSituacaoDto(vinculoId, ano, r.St, r.M1, r.MF, r.P1, r.P2);
    }

    // --------------------------- PARAMETROS (regras 11/22) ------------------------------
    public async Task<IReadOnlyList<ParametroEfetivoDto>> ParametrosEfetivosAsync(PrevidenciaContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var rows = await c.QueryAsync<(string Cod, string Nome, string Tipo, string Desc, string Padrao, string? Ov)>(new CommandDefinition(
            """
            select p.codigo, p.nome, p.tipo, p.descricao, p.valor_padrao::text,
                   (select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=p.id and not val.is_deleted limit 1)
            from sigov.parametro_modulo p where p.modulo='PREVIDENCIA' and p.ativo and not p.is_deleted order by p.ordem
            """, new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => new ParametroEfetivoDto(r.Cod, r.Nome, r.Tipo, r.Ov ?? r.Padrao, r.Ov is null ? "PADRAO" : "OVERRIDE", r.Desc)).ToList();
    }

    // --------------------------- ESOCIAL PUBLICO (BR-12.8) -----------------------------
    // Lote unico por tenant x competencia x tipo: gerar de novo devolve o MESMO lote
    // (idempotente); validacao previa ao envio — erro bloqueia (sem sucesso simulado).
    static readonly string[] LotesValidos = { "TABELAS_S1000_S1010", "ADMISAO_S1005", "MOVIMENTACAO_S1006", "REMUNERACAO_S1200" };

    public async Task<EsocialLoteDto> GerarLoteEsocialAsync(PrevidenciaContext x, EsocialLoteGerarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        if (r.Competencia.Day != 1) throw new CompetenciaInvalidaException();
        if (!LotesValidos.Contains(r.TipoLote)) throw new ArgumentException("LOTE_TIPO_INVALIDO: use TABELAS_S1000_S1010, ADMISAO_S1005, MOVIMENTACAO_S1006 ou REMUNERACAO_S1200.");
        using var c = factory.CreateConnection();
        var comp = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.previdencia_folha_competencia where tenant_id=@T and competencia=@C",
            new { T = x.TenantId, C = r.Competencia }, cancellationToken: ct)).ConfigureAwait(false);
        if (comp.Id == 0) throw new FolhaCompetenciaInexistenteException();
        if (r.TipoLote == "REMUNERACAO_S1200" && comp.Status != "FECHADA") throw new FolhaNaoFechadaParaLoteException();
        var compId = comp.Id;

        var existente = await c.QueryFirstOrDefaultAsync<(long Id, int Versao, string Status, int Total, int Erros, DateTime Gerado, DateTime? Enviado)>(new CommandDefinition(
            "select id, versao, status, total_eventos, eventos_com_erro, gerado_em, enviado_em from sigov.previdencia_esocial_lote where tenant_id=@T and competencia=@C and lote_tipo=@L",
            new { T = x.TenantId, C = r.Competencia, L = r.TipoLote }, cancellationToken: ct)).ConfigureAwait(false);
        if (existente.Id != 0)
            return new EsocialLoteDto(existente.Id, r.Competencia, r.TipoLote, existente.Versao, existente.Status, existente.Total, existente.Erros, existente.Gerado, existente.Enviado);

        var orgao = await c.QueryFirstOrDefaultAsync<(string Nome, string? Esfera, string? Tipo)>(new CommandDefinition(
            "select nome, esfera_governo, tipo_entidade from sigov.entidade where id=@E and tenant_id=@T",
            new { E = x.EntidadeId ?? throw new EntidadeSemEsferaException(), T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(orgao.Nome)) throw new PrevidenciaRecursoNaoDisponivelException("entidade do contexto institucional ausente");

        var eventos = new List<(string Ev, string Alvo, object Payload, bool Ok, List<string> Msgs)>();
        if (r.TipoLote == "TABELAS_S1000_S1010")
        {
            eventos.Add(("S-1000", "ENTIDADE", new { orgao = orgao.Nome, esfera = orgao.Esfera, tipo_entidade = orgao.Tipo }, !string.IsNullOrWhiteSpace(orgao.Nome),
                string.IsNullOrWhiteSpace(orgao.Nome) ? new List<string> { "S-1000: entidade sem nome" } : new List<string>()));
            var cargos = await c.QueryAsync<(string Cod, string Nome)>(new CommandDefinition(
                "select codigo_externo, nome from sigov.previdencia_cargo where tenant_id=@T and ativo and not is_deleted order by codigo_externo",
                new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var g in cargos)
                eventos.Add(("S-1010", g.Cod, new { codigo = g.Cod, nome = g.Nome }, !string.IsNullOrWhiteSpace(g.Cod), string.IsNullOrWhiteSpace(g.Cod) ? new List<string> { "S-1010: cargo sem codigo" } : new List<string>()));
        }
        else if (r.TipoLote == "ADMISAO_S1005")
        {
            var servidores = await c.QueryAsync<(string Mat, string Nome, string Cg, DateOnly Ad)>(new CommandDefinition(
                """
                select s.matricula_funcional, s.nome, g.codigo_externo, v.data_admissao
                from sigov.previdencia_vinculo v join sigov.previdencia_servidor s on s.id=v.servidor_id
                join sigov.previdencia_cargo g on g.id=v.cargo_id
                where v.tenant_id=@T and not v.is_deleted and v.data_admissao<=@DF order by s.matricula_funcional
                """, new { T = x.TenantId, DF = r.DataFixa }, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var sv in servidores)
            {
                var msgs = new List<string>();
                if (sv.Mat.Length < 3) msgs.Add("S-1005: matricula_funcional curta (<3 caracteres)");
                if (sv.Nome.Length < 5) msgs.Add("S-1005: nome do servidor curto (<5 caracteres)");
                eventos.Add(("S-1005", sv.Mat, new { matricula = sv.Mat, nome = sv.Nome, cargo = sv.Cg, admissao = sv.Ad }, msgs.Count == 0, msgs));
            }
        }
        else if (r.TipoLote == "MOVIMENTACAO_S1006")
        {
            var afast = await c.QueryAsync<(string Mat, string Tipo, DateOnly I, DateOnly F, string Ged)>(new CommandDefinition(
                """
                select s.matricula_funcional, a.tipo, a.data_inicio, a.data_fim, a.documento_ged
                from sigov.previdencia_afastamento a
                join sigov.previdencia_vinculo v on v.id=a.vinculo_id
                join sigov.previdencia_servidor s on s.id=v.servidor_id
                where a.tenant_id=@T and extract(year from a.data_inicio)=@Y and extract(month from a.data_inicio)=@M
                order by a.data_inicio
                """, new { T = x.TenantId, Y = r.Competencia.Year, M = r.Competencia.Month }, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var a in afast)
            {
                var ok = !string.IsNullOrWhiteSpace(a.Ged);
                eventos.Add(("S-1006", a.Mat + "-" + a.Tipo, new { matricula = a.Mat, evento = "AFASTAMENTO", tipo = a.Tipo, inicio = a.I, fim = a.F, documento_ged = a.Ged }, ok,
                    ok ? new List<string>() : new List<string> { "S-1006: afastamento sem documento GED" }));
            }
        }
        else
        {
            var calcs = await c.QueryAsync<(string Mat, decimal Bruto, decimal Liq, int Versao)>(new CommandDefinition(
                """
                select s.matricula_funcional, k.bruto, k.liquido, k.versao
                from sigov.previdencia_folha_calculo k
                join sigov.previdencia_vinculo v on v.id=k.vinculo_id
                join sigov.previdencia_servidor s on s.id=v.servidor_id
                where k.tenant_id=@T and k.competencia_id=@C and k.status='VIGENTE' order by s.matricula_funcional
                """, new { T = x.TenantId, C = compId }, cancellationToken: ct)).ConfigureAwait(false);
            foreach (var k in calcs)
            {
                var ok = k.Bruto > 0 && k.Liq >= 0;
                eventos.Add(("S-1200", k.Mat, new { matricula = k.Mat, bruto = k.Bruto, liquido = k.Liq, versao_calculo = k.Versao }, ok,
                    ok ? new List<string>() : new List<string> { "S-1200: valores inconsistentes (bruto<=0 ou liquido<0)" }));
            }
        }

        var erros = eventos.Count(e => !e.Ok);
        long loteId;
        using (var tx = c.BeginTransaction())
        {
            loteId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.previdencia_esocial_lote(tenant_id,competencia,lote_tipo,versao,status,total_eventos,eventos_com_erro,created_by)
                values (@T,@C,@L,1,'GERADO',@Nt,@Er,@U) returning id
                """, new { T = x.TenantId, C = r.Competencia, L = r.TipoLote, Nt = eventos.Count, Er = erros, U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            var seq = 0;
            foreach (var e in eventos)
            {
                seq++;
                var json = System.Text.Json.JsonSerializer.Serialize(e.Payload);
                await c.ExecuteAsync(new CommandDefinition(
                    """
                    insert into sigov.previdencia_esocial_evento(tenant_id,lote_id,sequencia,evento,alvo_texto,payload,validacao_ok,mensagens,created_by)
                    values (@T,@Lte,@Seq,@Ev,@Alvo,cast(@P as jsonb),@Ok,@Msgs,@U)
                    """, new { T = x.TenantId, Lte = loteId, Seq = seq, Ev = e.Ev, Alvo = e.Alvo, P = json, Ok = e.Ok, Msgs = e.Msgs.ToArray(), U = U(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
            tx.Commit();
        }
        return new EsocialLoteDto(loteId, r.Competencia, r.TipoLote, 1, "GERADO", eventos.Count, erros, clock.UtcNow.UtcDateTime, null);
    }

    public async Task<EsocialLoteDto> EnviarLoteEsocialAsync(PrevidenciaContext x, long loteId, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var l = await c.QueryFirstOrDefaultAsync<(long Id, DateOnly C, string L, int Versao, string St, int Total, int Erros, DateTime Gerado, DateTime? Enviado)>(new CommandDefinition(
            "select id, competencia, lote_tipo, versao, status, total_eventos, eventos_com_erro, gerado_em, enviado_em from sigov.previdencia_esocial_lote where id=@L and tenant_id=@T",
            new { L = loteId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (l.Id == 0) throw new LoteEsocialInexistenteException();
        if (l.St == "VALIDADO")
            return new EsocialLoteDto(l.Id, l.C, l.L, l.Versao, l.St, l.Total, l.Erros, l.Gerado, l.Enviado);
        if (l.Erros > 0)
        {
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.previdencia_esocial_lote set status='BLOQUEADO_VALIDACAO', updated_at=now() where id=@L", new { L = loteId }, cancellationToken: ct)).ConfigureAwait(false);
            throw new LoteEsocialBloqueadoValidacaoException(l.Erros);
        }
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.previdencia_esocial_lote set status='VALIDADO', enviado_em=now(), updated_at=now() where id=@L", new { L = loteId }, cancellationToken: ct)).ConfigureAwait(false);
        var st = await c.QueryFirstAsync<DateTime?>(new CommandDefinition("select enviado_em from sigov.previdencia_esocial_lote where id=@L", new { L = loteId }, cancellationToken: ct)).ConfigureAwait(false);
        return new EsocialLoteDto(l.Id, l.C, l.L, l.Versao, "VALIDADO", l.Total, 0, l.Gerado, st);
    }

    public async Task<EsocialLoteDetalheDto> DetalharLoteEsocialAsync(PrevidenciaContext x, long loteId, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var l = await c.QueryFirstOrDefaultAsync<(long Id, DateOnly C, string L, int Versao, string St, int Total, int Erros, DateTime Gerado, DateTime? Enviado)>(new CommandDefinition(
            "select id, competencia, lote_tipo, versao, status, total_eventos, eventos_com_erro, gerado_em, enviado_em from sigov.previdencia_esocial_lote where id=@L and tenant_id=@T",
            new { L = loteId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (l.Id == 0) throw new LoteEsocialInexistenteException();
        var ev = (await c.QueryAsync<(int Seq, string Ev, string Alvo, bool Ok, string[] Msgs)>(new CommandDefinition(
            "select sequencia, evento, alvo_texto, validacao_ok, mensagens from sigov.previdencia_esocial_evento where lote_id=@L order by sequencia",
            new { L = loteId }, cancellationToken: ct)))
            .Select(r => new EsocialEventoDto(0, r.Seq, r.Ev, r.Alvo, r.Ok, r.Msgs.ToList())).ToList();
        return new EsocialLoteDetalheDto(new EsocialLoteDto(l.Id, l.C, l.L, l.Versao, l.St, l.Total, l.Erros, l.Gerado, l.Enviado), ev);
    }

    // ------------------------------ DASHBOARD ------------------------------------------
    public async Task<DashboardPrevidenciaDto> DashboardAsync(PrevidenciaContext x, CancellationToken ct)
    {
        await GuardAsync(ct).ConfigureAwait(false);
        using var c = factory.CreateConnection();
        var esfera = x.EntidadeId.HasValue
            ? await c.ExecuteScalarAsync<string?>(new CommandDefinition("select esfera_governo from sigov.entidade where id=@E and tenant_id=@T", new { E = x.EntidadeId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)
            : null;
        var q = new (string Nome, int Val)[]
        {
            ("cargos", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_cargo where tenant_id=@T and ativo and not is_deleted", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("servidores", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_vinculo v where v.tenant_id=@T and v.situacao='ATIVO' and not v.is_deleted", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("fechadas", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_folha_competencia where tenant_id=@T and status='FECHADA'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("calc", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_folha_calculo where tenant_id=@T and status='VIGENTE'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("ferias", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_ferias where tenant_id=@T and saldo_dias>0", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("afast", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_afastamento where tenant_id=@T and status='ATIVO'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("lotes", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_esocial_lote where tenant_id=@T and status='GERADO'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("validados", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_esocial_lote where tenant_id=@T and status='VALIDADO'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
            ("bloqueados", await c.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from sigov.previdencia_esocial_lote where tenant_id=@T and status='BLOQUEADO_VALIDACAO'", new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false)),
        };
        var liq = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(k.liquido),0) from sigov.previdencia_folha_calculo k
            join sigov.previdencia_folha_competencia f on f.id=k.competencia_id
            where k.tenant_id=@T and k.status='VIGENTE' and f.competencia=(select max(competencia) from sigov.previdencia_folha_competencia where tenant_id=@T and status='FECHADA')
            """, new { T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return new DashboardPrevidenciaDto(esfera, q[0].Val, q[1].Val, q[2].Val, q[3].Val, q[4].Val, q[5].Val, q[6].Val, q[7].Val, q[8].Val, liq);
    }
}
