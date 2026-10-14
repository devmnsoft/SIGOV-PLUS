using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sigov.Application.Abstractions;
using Sigov.Application.Compras.Integradas;
using Sigov.Application.Health;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Compras;

// RC-EVO Ciclo 2 Sprint 8 (BR-8.1 a BR-8.7): comandos tipados da frente publica de compras
// multi-esfera sobre as tabelas do schema generico (compras_*) + deltas idempotentes da migration
// 20261013140000. Banco = autoridade (regras 11/12): modalidades/dispensas/parametros lidos do
// catalogo; parametros por tenant com override em parametro_modulo_valor (mesmo padrao EDUCACAO).
// Falhas nomeadas explicitas (regra 13): schema ausente -> COMPRAS_INTEGRADAS_NAO_DISPONIVEL.
public sealed class ComprasIntegradasRepository(NpgsqlConnectionFactory factory, IDatabaseObjectInspector inspector, IDateTimeProvider clock, ILogger<ComprasIntegradasRepository> log) : IComprasIntegradasService
{
    static readonly string[] ModalidadesDiretas = { "DISPENSA", "INEXIGIBILIDADE", "CONSULTA_PREVIA" };

    async Task GuardAsync(CancellationToken ct, params string[] tabelas)
    {
        foreach (var t in tabelas)
            if (!await inspector.TableExistsAsync("sigov", t, ct).ConfigureAwait(false))
                throw new ComprasIntegradasRecursoNaoDisponivelException(t);
    }

    static string SqlParametro =>
        """
        select coalesce((select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=c.pid and not val.is_deleted limit 1), c.vp)
        from (select id as pid, valor_padrao::text as vp from sigov.parametro_modulo where modulo='COMPRAS' and codigo=@Cod and ativo and not is_deleted limit 1) c
        """;

    async Task<long> LireParametroIntAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, string codigo, CancellationToken ct)
    {
        var v = await c.ExecuteScalarAsync<string>(new CommandDefinition(SqlParametro, new { T = x.TenantId, Cod = codigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v) || !long.TryParse(v, out var n))
            throw new ComprasIntegradasRecursoNaoDisponivelException("parametro_modulo.COMPRAS." + codigo);
        return n;
    }

    // Sprint 10 (BR-10.3): parametros do modulo CONVENIOS (mesmo padrao de override por tenant).
    static string SqlParametroConvenios =>
        """
        select coalesce((select val.valor::text from sigov.parametro_modulo_valor val where val.tenant_id=@T and val.parametro_id=c.pid and not val.is_deleted limit 1), c.vp)
        from (select id as pid, valor_padrao::text as vp from sigov.parametro_modulo where modulo='CONVENIOS' and codigo=@Cod and ativo and not is_deleted limit 1) c
        """;

    async Task<long> LireParametroIntConveniosAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, string codigo, CancellationToken ct)
    {
        var v = await c.ExecuteScalarAsync<string>(new CommandDefinition(SqlParametroConvenios, new { T = x.TenantId, Cod = codigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v) || !long.TryParse(v, out var n))
            throw new ComprasIntegradasRecursoNaoDisponivelException("parametro_modulo.CONVENIOS." + codigo);
        return n;
    }

    async Task<decimal> LireParametroDecimalAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, string codigo, CancellationToken ct)
    {
        var v = await c.ExecuteScalarAsync<string>(new CommandDefinition(SqlParametro, new { T = x.TenantId, Cod = codigo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(v) || !decimal.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out var d))
            throw new ComprasIntegradasRecursoNaoDisponivelException("parametro_modulo.COMPRAS." + codigo);
        return d;
    }

    // Contexto institucional multi-esfera (regras 21-24): a entidade do tenant traz esfera/unidades;
    // sem esfera configurada a operacao falha explicitamente (regra 13) - nada municipal hardcoded.
    sealed record EntCtx(string Esfera, long Gestora, long Executora, long? OrgaoSuperior);
    async Task<EntCtx> ContextoEntidadeAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, CancellationToken ct)
    {
        var e = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var r = await c.QueryFirstOrDefaultAsync<(string Esfera, long Gestora, long Executora, long? OrgaoSuperior)>(new CommandDefinition(
            """
            select e.esfera_governo, coalesce(e.unidade_gestora_id, e.id), coalesce(e.unidade_executora_id, e.id), e.orgao_superior_id
            from sigov.entidade e where e.id=@E and e.tenant_id=@T and e.ativo=true and not e.is_deleted
            """, new { E = e, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Esfera))
            throw new ComprasIntegradasRecursoNaoDisponivelException("entidade.esfera_governo");
        return new EntCtx(r.Esfera.Trim(), r.Gestora, r.Executora, r.OrgaoSuperior);
    }

    Guid GuidCorr(string corr) => Guid.TryParse(corr, out var g) ? g : Guid.NewGuid();
    long UsuarioObrigatorio(ComprasIntegradasContext x) => x.UsuarioId ?? throw new InvalidOperationException("usuário obrigatório no contexto.");

    // -------------------------------- DEMANDA (BR-8.1) -------------------------------
    // CriadoEm como DateTime (e nao DateTimeOffset): o Npgsql traz timestamptz como DateTime(Utc) e o
    // bind posicional do Dapper exige assinatura exata do construtor - com DateTimeOffset nenhuma
    // chamada que materializa DemRow (aprovar/repelir/cancelar/reabrir) encontrava o construtor e
    // virava 409 genérico via InvalidOperationException (descoberto no E2E rodadas 5-7).
    // CentroCustoId/AprovadoPor/DuplaAprovadoPor sao long NAO nulos com coalesce(0): o match de
    // construtor do Dapper nao aceita Nullable<Int64> para colunas Int64 do grid (mesma classe de
    // falha); 0 = ausente (ids reais nunca sao 0).
    sealed record DemRow(long Id, string Objeto, string Status, bool Urgencia, decimal ValorEstimado, long CentroCustoId, long CriadoPor, long AprovadoPor, long DuplaAprovadoPor, DateTime CriadoEm);

    async Task<DemRow> LoadDemandaAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        var d = await c.QueryFirstOrDefaultAsync<DemRow>(new CommandDefinition(
            """
            select id, objeto, status, urgencia, valor_estimado as ValorEstimado,
                   coalesce(centro_custo_id,0) as CentroCustoId, created_by as CriadoPor,
                   coalesce(aprovado_por,0) as AprovadoPor, coalesce(dupla_aprovado_por,0) as DuplaAprovadoPor, created_at as CriadoEm
            from sigov.compras_demanda where id=@Id and tenant_id=@T
            """, new { Id = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return d ?? throw new DemandaInexistenteException(id);
    }

    async Task<DemandaIntegradaDto> ToDtoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        var d = await LoadDemandaAsync(c, tx, x, id, ct).ConfigureAwait(false);
        var r = await c.QueryFirstAsync<(int NItens, long? ProcessoId)>(new CommandDefinition(
            """
            select (select count(1) from sigov.compras_demanda_item i where i.demanda_id=@Id and i.ativo=true and not i.is_deleted),
                   (select p.id from sigov.compras_processo p where p.tenant_id=@T and p.demanda_id=@Id and p.status not in ('CANCELADO','FRACASSADO','DESERTO') limit 1)
            """, new { Id = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new DemandaIntegradaDto(d.Id, d.Objeto, d.Status, d.Urgencia, d.ValorEstimado, r.NItens, d.CentroCustoId == 0 ? null : d.CentroCustoId, r.ProcessoId, d.CriadoPor, d.AprovadoPor == 0 ? null : d.AprovadoPor, d.DuplaAprovadoPor == 0 ? null : d.DuplaAprovadoPor, d.CriadoEm.ToUniversalTime());
    }

    public async Task<IReadOnlyList<DemandaIntegradaDto>> DemandasAsync(ComprasIntegradasContext x, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda", "compras_demanda_item", "compras_processo").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        // Projeção posicional explícita (mesmo padrão do ToDtoAsync, L108): alias camelCase são
        // minuscualizados pelo PG e não casam com os parâmetros do record no binder por nome do
        // Dapper — defeito C2S11 (GET /demandas retornava 409 de materialização).
        var rows = (await c.QueryAsync<(long Id, string Objeto, string Status, bool Urgencia, decimal ValorEstimado, int NItens, long? CentroCustoId, long? ProcessoId, long CriadoPor, long? AprovadoPor, long? DuplaAprovadoPor, DateTime CriadoEm)>(new CommandDefinition(
            """
            select d.id, d.objeto, d.status, d.urgencia, d.valor_estimado,
                   (select count(1) from sigov.compras_demanda_item i where i.demanda_id=d.id and i.ativo=true and not i.is_deleted),
                   d.centro_custo_id,
                   (select p.id from sigov.compras_processo p where p.tenant_id=d.tenant_id and p.demanda_id=d.id and p.status not in ('CANCELADO','FRACASSADO','DESERTO') limit 1),
                   d.created_by, d.aprovado_por, d.dupla_aprovado_por, d.created_at
            from sigov.compras_demanda d where d.tenant_id=@T and (@E::bigint is null or d.entidade_id=@E)
            order by d.created_at desc limit 200
            """, new { T = x.TenantId, E = x.EntidadeId }, cancellationToken: ct))).AsList();
        return rows.Select(r => new DemandaIntegradaDto(r.Id, r.Objeto, r.Status, r.Urgencia, r.ValorEstimado, r.NItens, r.CentroCustoId, r.ProcessoId, r.CriadoPor, r.AprovadoPor, r.DuplaAprovadoPor, r.CriadoEm.ToUniversalTime())).ToList();
    }

    public async Task<DemandaIntegradaDto> DemandaAsync(ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda", "compras_processo").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        return await ToDtoAsync(c, null, x, id, ct).ConfigureAwait(false);
    }

    public async Task<long> CriarDemandaAsync(ComprasIntegradasContext x, DemandaCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda", "compras_demanda_item", "financeiro_centro_custo").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Objeto)) throw new DemandaItemInvalidoException("objeto é obrigatório.");
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new DemandaItemInvalidoException("justificativa é obrigatória.");
        if (r.Itens is null || r.Itens.Count == 0) throw new DemandaItemInvalidoException("pelo menos um item é obrigatório.");
        foreach (var it in r.Itens)
        {
            if (string.IsNullOrWhiteSpace(it.Descricao)) throw new DemandaItemInvalidoException("item sem descrição.");
            if (it.Quantidade <= 0) throw new DemandaItemInvalidoException($"quantidade inválida ({it.Quantidade}) - deve ser > 0.");
            if (string.IsNullOrWhiteSpace(it.Unidade)) throw new DemandaItemInvalidoException("item sem unidade conhecida.");
            var tipo = string.IsNullOrWhiteSpace(it.TipoObjeto) ? "BEM" : it.TipoObjeto.Trim().ToUpperInvariant();
            if (tipo is not ("BEM" or "SERVICO" or "OBRA"))
                throw new DemandaItemInvalidoException($"tipo de objeto inválido ('{it.TipoObjeto}') - use BEM, SERVICO ou OBRA.");
            if (it.PrecoEstimado < 0) throw new DemandaItemInvalidoException("preço estimado não pode ser negativo.");
            if (it.PrecoEstimado <= 0 && it.PesquisaPrecoId is null)
                throw new DemandaItemInvalidoException("item exige preço estimado > 0 ou pesquisa de preços referenciada (pesquisaPrecoId).");
            if (it.CentroCustoId <= 0) throw new DemandaItemInvalidoException("item sem centro de custo válido.");
        }
        if (r.Urgencia && string.IsNullOrWhiteSpace(r.JustificativaUrgencia))
            throw new DemandaUrgenciaSemJustificativaException();

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var ent = await ContextoEntidadeAsync(c, tx, x, ct).ConfigureAwait(false);
        var exercicio = x.ExercicioId ?? clock.DataAtual.Year;

        // centros de custo validos e ativos no tenant (banco = autoridade)
        var ccIds = r.Itens.Select(i => i.CentroCustoId).Distinct().ToList();
        var ccOk = (await c.QueryAsync<long>(new CommandDefinition(
            "select id from sigov.financeiro_centro_custo where tenant_id=@T and ativo=true and id = any(@Ids)",
            new { T = x.TenantId, Ids = ccIds }, tx, cancellationToken: ct))).ToList().ToHashSet();
        var ccFalta = ccIds.FirstOrDefault(i => !ccOk.Contains(i));
        if (ccFalta > 0) throw new DemandaItemInvalidoException($"centro de custo {ccFalta} inexistente/inativo no tenant.");

        var valorTotal = Math.Round(r.Itens.Sum(i => i.Quantidade * i.PrecoEstimado), 2);
        var user = UsuarioObrigatorio(x);
        var corr = GuidCorr(x.CorrelationId);
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_demanda(tenant_id,entidade_id,esfera_governo,unidade_gestora_id,unidade_executora_id,exercicio_id,objeto,justificativa,valor_estimado,prioridade,status,urgencia,justificativa_urgencia,centro_custo_id,created_at,updated_at,created_by,updated_by)
            values(@T,@E,@Esfera,@Gestora,@Executora,@Ex,@Objeto,@Justif,@Valor,'NORMAL','SOLICITADA',@Urg,@JustUrg,@Cc,now(),now(),@U,@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, Esfera = ent.Esfera, Gestora = ent.Gestora, Executora = ent.Executora, Ex = exercicio, Objeto = r.Objeto.Trim(), Justif = r.Justificativa.Trim(), Valor = valorTotal, Urg = r.Urgencia, JustUrg = r.JustificativaUrgencia, Cc = r.Itens[0].CentroCustoId, U = user, correlation_id = corr }, tx, cancellationToken: ct)).ConfigureAwait(false);

        int seq = 0;
        foreach (var it in r.Itens)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_demanda_item(tenant_id,entidade_id,esfera_governo,exercicio_id,demanda_id,sequencia,descricao,quantidade,unidade,tipo_objeto,preco_estimado,pesquisa_preco_id,centro_custo_id,correlation_id,created_at,created_by)
                values(@T,@E,@Esfera,@Ex,@Dem,@Seq,@Desc,@Qtd,@Un,@Tipo,@Preco,@Pesq,@Cc,@Corr,now(),@U)
                """, new
                {
                    T = x.TenantId, E = x.EntidadeId, Esfera = ent.Esfera, Ex = exercicio, Dem = id, Seq = ++seq,
                    Desc = it.Descricao.Trim(), Qtd = it.Quantidade, Un = it.Unidade.Trim(),
                    Tipo = it.TipoObjeto?.Trim().ToUpperInvariant() ?? "BEM", Preco = it.PrecoEstimado, Pesq = it.PesquisaPrecoId, Cc = it.CentroCustoId, Corr = corr, U = user
                }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 demanda {Id} criada ({N} itens, urgencia={Urg})", id, r.Itens.Count, r.Urgencia);
        return id;
    }

    // garante o processo unico vinculado a demanda APROVADA (BR-8.2, Lei 14.133 art. 128)
    async Task<long> GarantirProcessoUnicoAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, DemRow d, CancellationToken ct)
    {
        var existente = await c.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            "select id from sigov.compras_processo where tenant_id=@T and demanda_id=@D and status not in ('CANCELADO','FRACASSADO','DESERTO') limit 1",
            new { T = x.TenantId, D = d.Id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (existente.HasValue) return existente.Value;
        return await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_processo(tenant_id,entidade_id,exercicio,numero,objeto,justificativa,valor_estimado,status,data_abertura,documentos_metadados,demanda_id,created_at,updated_at,created_by)
            values(@T,@E,@Ex,'PI-'||to_char(now(),'YYYY')||'-'||lpad(@D::text,10,'0'),@Objeto,'Demanda tipada aprovada (BR-8.2 - processo unico da contratacao, Lei 14.133 art. 128)',@Valor,'ABERTO',current_date,'{}'::jsonb,@D,now(),now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, Ex = clock.DataAtual.Year, D = d.Id, Objeto = d.Objeto, Valor = d.ValorEstimado, U = UsuarioObrigatorio(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    void VerificarSeparacaoSolicitanteAprovador(DemRow d, long usuario)
    {
        if (usuario == d.CriadoPor)
            throw new SeparacaoFuncoesVioladaException($"solicitante não pode aprovar/decidir a própria demanda (usuario {usuario}).");
    }

    async Task AtualizarDemandaStatusAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, DemRow d, string novoStatus, string? justificativaDecisao, long? aprovadoPor, long? duplaAprovadoPor, CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(
            """
            update sigov.compras_demanda set status=@St, justificativa_decisao=@JD,
               aprovado_em=case when @AP is not null then now() else aprovado_em end, aprovado_por=coalesce(@AP, aprovado_por),
               dupla_aprovado_em=case when @DP is not null then now() else dupla_aprovado_em end, dupla_aprovado_por=coalesce(@DP, dupla_aprovado_por),
               updated_at=now(), updated_by=@U
            where id=@Id and tenant_id=@T
            """, new { St = novoStatus, JD = justificativaDecisao, AP = aprovadoPor, DP = duplaAprovadoPor, U = UsuarioObrigatorio(x), Id = d.Id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<DemandaIntegradaDto> DemandaAprovarAsync(ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda", "compras_processo").ConfigureAwait(false);
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await ContextoEntidadeAsync(c, tx, x, ct).ConfigureAwait(false);
        var d = await LoadDemandaAsync(c, tx, x, id, ct).ConfigureAwait(false);

        switch (d.Status)
        {
            case "SOLICITADA":
                VerificarSeparacaoSolicitanteAprovador(d, user);
                if (!d.Urgencia)
                {
                    await AtualizarDemandaStatusAsync(c, tx, x, d, "APROVADA", null, user, null, ct).ConfigureAwait(false);
                    await GarantirProcessoUnicoAsync(c, tx, x, d, ct).ConfigureAwait(false);
                }
                else
                {
                    // BR-8.1: urgencia exige dupla aprovacao por autoridades distintas
                    await AtualizarDemandaStatusAsync(c, tx, x, d, "APROVADA_PARCIAL", null, user, null, ct).ConfigureAwait(false);
                }
                break;
            case "APROVADA_PARCIAL":
                if (user == d.AprovadoPor)
                    throw new SeparacaoFuncoesVioladaException($"segunda aprovação de urgência exige autoridade distinta da primeira (usuario {user}).");
                await AtualizarDemandaStatusAsync(c, tx, x, d, "APROVADA", null, user, user, ct).ConfigureAwait(false);
                await GarantirProcessoUnicoAsync(c, tx, x, d, ct).ConfigureAwait(false);
                break;
            case "APROVADA":
                throw new DemandaAposAprovacaoImutavelException(id);
            default:
                throw new DemandaEstadoInvalidoException(id, d.Status, "aprovar");
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        // tx ja foi commitada: a leitura do DTO roda sem transacao explicita (mesmo padrao da DemandaAsync,
        // linha acima) - usar a NpgsqlTransaction apos CommitAsync lanca "Transaction is already completed"
        // (falha nomeada que virava 409 genérico no E2E, rodadas 5-7).
        return await ToDtoAsync(c, null, x, id, ct).ConfigureAwait(false);
    }

    async Task<DemandaIntegradaDto> DecisaoRepelirOuCancelarAsync(ComprasIntegradasContext x, long id, string justificativa, string destino, CancellationToken ct)
    {
        var user = UsuarioObrigatorio(x);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException($"Justificativa é obrigatória para {destino}.");
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var d = await LoadDemandaAsync(c, tx, x, id, ct).ConfigureAwait(false);
        if (d.Status is "APROVADA" or "APROVADA_PARCIAL") throw new DemandaAposAprovacaoImutavelException(id);
        if (d.Status != "SOLICITADA") throw new DemandaEstadoInvalidoException(id, d.Status, destino);
        VerificarSeparacaoSolicitanteAprovador(d, user);
        await AtualizarDemandaStatusAsync(c, tx, x, d, destino, justificativa.Trim(), null, null, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        // tx ja foi commitada: a leitura do DTO roda sem transacao explicita (mesmo padrao da DemandaAsync,
        // linha acima) - usar a NpgsqlTransaction apos CommitAsync lanca "Transaction is already completed"
        // (falha nomeada que virava 409 genérico no E2E, rodadas 5-7).
        return await ToDtoAsync(c, null, x, id, ct).ConfigureAwait(false);
    }

    public async Task<DemandaIntegradaDto> DemandaRepelirAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda").ConfigureAwait(false);
        return await DecisaoRepelirOuCancelarAsync(x, id, justificativa, "REPELIDA", ct).ConfigureAwait(false);
    }

    public async Task<DemandaIntegradaDto> DemandaCancelarAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda").ConfigureAwait(false);
        return await DecisaoRepelirOuCancelarAsync(x, id, justificativa, "CANCELADA", ct).ConfigureAwait(false);
    }

    public async Task<DemandaIntegradaDto> DemandaReabrirAsync(ComprasIntegradasContext x, long id, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Justificativa de reabertura é obrigatória.");
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var d = await LoadDemandaAsync(c, tx, x, id, ct).ConfigureAwait(false);
        if (d.Status != "REPELIDA")
        {
            if (d.Status is "APROVADA" or "APROVADA_PARCIAL") throw new DemandaAposAprovacaoImutavelException(id);
            throw new DemandaEstadoInvalidoException(id, d.Status, "reabrir");
        }
        await c.ExecuteAsync(new CommandDefinition(
            """
            update sigov.compras_demanda set status='SOLICITADA', justificativa_decisao=@JD,
               aprovado_em=null, aprovado_por=null, dupla_aprovado_em=null, dupla_aprovado_por=null,
               updated_at=now(), updated_by=@U
            where id=@Id and tenant_id=@T
            """, new { JD = justificativa.Trim(), U = UsuarioObrigatorio(x), Id = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        // tx ja foi commitada: a leitura do DTO roda sem transacao explicita (mesmo padrao da DemandaAsync,
        // linha acima) - usar a NpgsqlTransaction apos CommitAsync lanca "Transaction is already completed"
        // (falha nomeada que virava 409 genérico no E2E, rodadas 5-7).
        return await ToDtoAsync(c, null, x, id, ct).ConfigureAwait(false);
    }

    // ------------------------- PROCEDIMENTO (BR-8.2/8.3/8.6) ---------------------------
    public async Task<ProcedimentoAbertoDto> ProcedimentoAbrirAsync(ComprasIntegradasContext x, ProcedimentoAbrirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_demanda", "compras_processo", "compras_licitacao", "compras_modalidade_config", "compras_dispensa_base_legal").ConfigureAwait(false);
        if (r.DemandaId <= 0) throw new ArgumentException("demandaId é obrigatório.");
        if (string.IsNullOrWhiteSpace(r.ModalidadeCodigo)) throw new ArgumentException("modalidadeCodigo é obrigatória.");
        var modulo = r.ModalidadeCodigo.Trim().ToUpperInvariant();

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var ent = await ContextoEntidadeAsync(c, tx, x, ct).ConfigureAwait(false);
        var d = await LoadDemandaAsync(c, tx, x, r.DemandaId, ct).ConfigureAwait(false);
        if (d.Status == "APROVADA_PARCIAL") throw new DemandaUrgenciaSemDuplaAprovacaoException(d.Id);
        if (d.Status != "APROVADA") throw new DemandaEstadoInvalidoException(d.Id, d.Status, "abrir procedimento");

        var pid = await GarantirProcessoUnicoAsync(c, tx, x, d, ct).ConfigureAwait(false);

        // licitacao ja aberta no processo? (idempotencia + procedimento unico)
        var existente = await c.QueryFirstOrDefaultAsync<(long Id, string Modalidade, string Critero)>(new CommandDefinition(
            """
            select l.id, cfg.modalidade, cfg.criterio_julgamento from sigov.compras_licitacao l
            join sigov.compras_modalidade_config cfg on cfg.id=l.modalidade_config_id
            where l.tenant_id=@T and l.processo_id=@P limit 1
            """, new { T = x.TenantId, P = pid }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (existente.Id > 0)
        {
            if (existente.Modalidade == modulo)
                return new ProcedimentoAbertoDto(pid, existente.Id, existente.Modalidade, existente.Critero, true);
            throw new ProcessoJaVinculadoADemandaException(d.Id, existente.Modalidade);
        }

        // catalogo de modalidades do banco (regras 11/12): linha de esfera explicita prioriza a global
        var cfg = await c.QueryFirstOrDefaultAsync<(long Id, string Critero)>(new CommandDefinition(
            """
            select id, criterio_julgamento from sigov.compras_modalidade_config
            where tenant_id=@T and modalidade=@Mod and ativo=true and entidade_id is null
              and (esfera_governo is null or esfera_governo=@Esfera)
            order by case when esfera_governo is null then 1 else 0 end limit 1
            """, new { T = x.TenantId, Mod = modulo, Esfera = ent.Esfera }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (cfg.Id <= 0) throw new ModalidadeForaDoCatalogoException(modulo);

        var total = r.OrcamentoEstimado ?? d.ValorEstimado;
        if (modulo == "CONSULTA_PREVIA")
        {
            var teto = await LireParametroDecimalAsync(c, tx, x, "COMPRAS_CONSULTA_PREVIA_TETO", ct).ConfigureAwait(false);
            if (total > teto) throw new ConsultaPreviaAcimaTetoException(total, teto);
        }
        if (modulo == "CONCORRENCIA")
        {
            var teto = await LireParametroDecimalAsync(c, tx, x, "COMPRAS_CONSULTA_PREVIA_TETO", ct).ConfigureAwait(false);
            if (total < teto)
                throw new ComprasIntegradasFalhaNomeada(new($"MODALIDADE_INCOMPATIVEL_OBJETO_VALOR: total {total:F2} abaixo do teto de consulta previa {teto:F2} não exige o regime de concorrência."));
        }

        string? baseLegal = null;
        if (ModalidadesDiretas.Contains(modulo))
        {
            if (modulo is ("DISPENSA" or "INEXIGIBILIDADE"))
            {
                if (string.IsNullOrWhiteSpace(r.DispensaBaseLegalCodigo)) throw new DispensaBaseLegalObrigatoriaException();
                var bl = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                    "select count(1) from sigov.compras_dispensa_base_legal where codigo=@C and ativo=true and not is_deleted",
                    new { C = r.DispensaBaseLegalCodigo.Trim().ToUpperInvariant() }, tx, cancellationToken: ct)).ConfigureAwait(false);
                if (bl == 0) throw new DispensaBaseLegalInexistenteException(r.DispensaBaseLegalCodigo);
                baseLegal = r.DispensaBaseLegalCodigo.Trim().ToUpperInvariant();
            }
        }

        var numero = "LIT-" + clock.DataAtual.Year + "-" + pid.ToString("D10");
        var lic = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_licitacao(tenant_id,entidade_id,esfera_governo,processo_id,orgao_id,unidade_gestora_id,unidade_executora_id,modalidade_config_id,numero,objeto,status,data_publicacao,data_disputa,created_at,updated_by,created_by)
            values(@T,@E,@Esfera,@P,@Org,@Gestora,@Exec,@Cfg,@Num,@Objeto,'ABERTA',current_date,now(),now(),@U,@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, Esfera = ent.Esfera, P = pid, Org = ent.OrgaoSuperior, Gestora = ent.Gestora, Exec = ent.Executora, Cfg = cfg.Id, Num = numero, Objeto = d.Objeto, U = UsuarioObrigatorio(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);

        // rastro do ato no processo unico (documentos_metadados jsonb append-only)
        var metadados = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["modalidade"] = modulo,
            ["criterio_julgamento"] = cfg.Critero,
            ["fonte_recurso"] = r.FonteRecurso,
            ["orcamento_estimado"] = total.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["dispensa_base_legal"] = baseLegal
        });
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_processo set documentos_metadados = coalesce(documentos_metadados,'{}'::jsonb) || @M::jsonb, updated_at=now(), updated_by=@U where id=@P and tenant_id=@T",
            new { M = metadados, U = UsuarioObrigatorio(x), P = pid, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 procedimento aberto: processo {P} licitacao {L} modalidade {M}", pid, lic, modulo);
        return new ProcedimentoAbertoDto(pid, lic, modulo, cfg.Critero, false);
    }

    // ------------------------------ HABILITACAO (BR-8.3) -------------------------------
    public async Task HabilitacaoJulgarAsync(ComprasIntegradasContext x, long licitacaoId, HabilitacaoJulgarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_habilitacao", "compras_fornecedor").ConfigureAwait(false);
        var tipo = string.IsNullOrWhiteSpace(r.Tipo) ? "" : r.Tipo.Trim().ToUpperInvariant();
        if (tipo is not ("JURIDICO" or "FISCAL_TRABALHISTA" or "TECNICO"))
            throw new ArgumentException("tipo inválido - use JURIDICO, FISCAL_TRABALHISTA ou TECNICO.");
        var st = string.IsNullOrWhiteSpace(r.Status) ? "" : r.Status.Trim().ToUpperInvariant();
        if (st is not ("APROVADO" or "REPROVADO")) throw new ArgumentException("status inválido - use APROVADO ou REPROVADO.");
        if (string.IsNullOrWhiteSpace(r.DocumentoRef)) throw new ArgumentException("documentoRef é obrigatório (conferência registrada).");
        if (st == "REPROVADO" && string.IsNullOrWhiteSpace(r.Justificativa))
            throw new HabilitacaoReprovacaoSemJustificativaException(r.FornecedorId, tipo);

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);
        var forn = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.compras_fornecedor where id=@F and tenant_id=@T",
            new { F = r.FornecedorId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (forn == 0) throw new FornecedorInexistenteException(r.FornecedorId);

        var justificativaTexto = "DOC:" + r.DocumentoRef.Trim() + (string.IsNullOrWhiteSpace(r.Justificativa) ? "" : " | " + r.Justificativa.Trim());
        var user = UsuarioObrigatorio(x);
        var existe = await c.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            "select id from sigov.compras_habilitacao where tenant_id=@T and licitacao_id=@L and fornecedor_id=@F and tipo=@Tipo and status not in ('ANULADO','CANCELADO') limit 1",
            new { T = x.TenantId, L = licitacaoId, F = r.FornecedorId, Tipo = tipo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (existe.HasValue)
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.compras_habilitacao set status=@St, justificativa=@Jd, responsavel_id=@U, updated_at=now(), updated_by=@U where id=@I",
                new { St = st, Jd = justificativaTexto, U = user, I = existe.Value }, tx, cancellationToken: ct)).ConfigureAwait(false);
        else
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_habilitacao(tenant_id,entidade_id,licitacao_id,fornecedor_id,tipo,status,justificativa,responsavel_id,created_at,updated_at,created_by,updated_by)
                values(@T,@E,@L,@F,@Tipo,@St,@Jd,@U,now(),now(),@U,@U)
                """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, F = r.FornecedorId, Tipo = tipo, St = st, Jd = justificativaTexto, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    // ------------------------------- LANCES (BR-8.3) -----------------------------------
    async Task<(long Id, string Status, string Zona, string Modalidade)> CarregarLicitacaoAsync(NpgsqlConnection c, NpgsqlTransaction? tx, ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        var r = await c.QueryFirstOrDefaultAsync<(long Id, string Status, string Zona, string Modalidade)>(new CommandDefinition(
            """
            select l.id, l.status, l.zona_me_epp_status, cfg.modalidade
            from sigov.compras_licitacao l
            left join sigov.compras_modalidade_config cfg on cfg.id=l.modalidade_config_id
            where l.id=@Id and l.tenant_id=@T
            """, new { Id = id, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (r.Id <= 0) throw new LicitacaoInexistenteException(id);
        return r;
    }

    public async Task<long> LanceRegistrarAsync(ComprasIntegradasContext x, long licitacaoId, LanceRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_lance", "compras_fornecedor").ConfigureAwait(false);
        if (r.Valor <= 0) throw new ArgumentException("valor do lance deve ser > 0.");
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lic = await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);
        if (lic.Status != "ABERTA") throw new SessaoLanceForaDeVigenciaException(licitacaoId, lic.Status);

        var forn = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select coalesce(categoria_me_epp,'') from sigov.compras_fornecedor where id=@F and tenant_id=@T",
            new { F = r.FornecedorId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (forn is null) throw new FornecedorInexistenteException(r.FornecedorId);
        var meEpp = forn == "ME" || forn == "EPP";

        // idempotencia por chave (1 lance = 1 registro): mesmo conteudo volta o efeito original
        if (!string.IsNullOrWhiteSpace(r.ChaveIdempotencia))
        {
            var previo = await c.QueryFirstOrDefaultAsync<(long Id, decimal Valor)>(new CommandDefinition(
                "select id, valor from sigov.compras_lance where tenant_id=@T and licitacao_id=@L and chave_idempotencia=@K and ativo=true and not is_deleted limit 1",
                new { T = x.TenantId, L = licitacaoId, K = r.ChaveIdempotencia.Trim() }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (previo.Id > 0)
            {
                if (Math.Round(previo.Valor, 2) == Math.Round(r.Valor, 2)) return previo.Id;
                throw new LanceChaveIdempotenciaConflitoException(r.ChaveIdempotencia, previo.Id);
            }
        }

        var melhor = await c.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "select min(valor) from sigov.compras_lance where tenant_id=@T and licitacao_id=@L and ativo=true and not is_deleted",
            new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (melhor.HasValue && r.Valor >= melhor.Value)
            throw new LanceNaoDecrescenteException(r.Valor, melhor.Value);

        var seq = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select coalesce(max(sequencia),0)+1 from sigov.compras_lance where tenant_id=@T and licitacao_id=@L and fornecedor_id=@F and ativo=true and not is_deleted",
            new { T = x.TenantId, L = licitacaoId, F = r.FornecedorId }, tx, cancellationToken: ct)).ConfigureAwait(false);

        var corr = GuidCorr(x.CorrelationId);
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_lance(tenant_id,entidade_id,licitacao_id,fornecedor_id,sequencia,valor,chave_idempotencia,correlation_id,created_at,created_by)
            values(@T,@E,@L,@F,@Seq,@V,@K,@Corr,now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, F = r.FornecedorId, Seq = seq, V = r.Valor, K = string.IsNullOrWhiteSpace(r.ChaveIdempotencia) ? null : r.ChaveIdempotencia.Trim(), Corr = corr, U = UsuarioObrigatorio(x) }, tx, cancellationToken: ct)).ConfigureAwait(false);

        if (meEpp)
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.compras_licitacao set zona_me_epp_status='ABERTA', updated_at=now() where id=@L and tenant_id=@T and zona_me_epp_status='NAO_APLICAVEL'",
                new { L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return id;
    }

    // ------------------------- ZONA ME/EPP (BR-8.3, LC 123/2006) -----------------------
    public async Task<PreferenciaDesfechoDto> PreferenciaDocumentarAsync(ComprasIntegradasContext x, long licitacaoId, PreferenciaDocumentarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_lance", "compras_fornecedor").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.DocumentoRef)) throw new ArgumentException("documentoRef é obrigatório (desfecho documentado).");
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lic = await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);
        if (lic.Zona == "NAO_APLICAVEL") throw new ZonaMeEppNaoAplicavelException(licitacaoId);
        if (lic.Zona is ("ENCERRADA_POR_PREFERENCIA" or "ENCERRADA_FORA_MARGEM"))
            throw new ZonaMeEppJaResolvidaException(licitacaoId, lic.Zona);

        var cat = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select coalesce(categoria_me_epp,'') from sigov.compras_fornecedor where id=@F and tenant_id=@T",
            new { F = r.FornecedorId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (cat is null) throw new FornecedorInexistenteException(r.FornecedorId);
        if (cat is not ("ME" or "EPP")) throw new FornecedorForaDaZonaMeEppException(r.FornecedorId);

        var margem = await LireParametroIntAsync(c, tx, x, "LICITACAO_MARGEM_ME_EPP_PCT", ct).ConfigureAwait(false);
        var melhorMe = await c.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            select min(l.valor) from sigov.compras_lance l
            join sigov.compras_fornecedor f on f.id=l.fornecedor_id
            where l.tenant_id=@T and l.licitacao_id=@L and l.ativo=true and not l.is_deleted
              and f.categoria_me_epp in ('ME','EPP')
            """, new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (melhorMe is null) throw new FornecedorForaDaZonaMeEppException(r.FornecedorId);
        var melhorOut = await c.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            select min(l.valor) from sigov.compras_lance l
            join sigov.compras_fornecedor f on f.id=l.fornecedor_id
            where l.tenant_id=@T and l.licitacao_id=@L and l.ativo=true and not l.is_deleted
              and (f.categoria_me_epp is null or f.categoria_me_epp not in ('ME','EPP'))
            """, new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);

        var limite = melhorOut.HasValue ? Math.Round(melhorOut.Value * (1m - margem / 100m), 2) : decimal.MaxValue;
        var dentro = melhorMe.Value <= limite;
        var zonaFinal = dentro ? "ENCERRADA_POR_PREFERENCIA" : "ENCERRADA_FORA_MARGEM";
        var desfecho = dentro ? "PREFERENCIA_EXERCIDA" : "FORA_DA_MARGEM_DESISTENCIA_DOCUMENTADA";
        var motivo = $"DOC:{r.DocumentoRef.Trim()} | melhor ME/EPP {melhorMe.Value:F2}; melhor fora zona {(melhorOut?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "sem lancista")}; margem parametrica {margem}% -> {desfecho}";

        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_licitacao set zona_me_epp_status=@Z, zona_motivo=@M, updated_at=now() where id=@L and tenant_id=@T",
            new { Z = zonaFinal, M = motivo, L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new PreferenciaDesfechoDto(zonaFinal, desfecho, dentro, melhorMe.Value, melhorOut, (int)margem);
    }

    // --------------------------- JULGAMENTO (BR-8.4) ----------------------------------
    public async Task<ClassificacaoDto> JulgamentoExecutarAsync(ComprasIntegradasContext x, long licitacaoId, string? justificativaEmpate, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_lance", "compras_proposta").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lic = await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);
        if (lic.Status == "JULGADA") throw new JulgamentoJaRealizadoException(licitacaoId);
        if (lic.Status != "ABERTA") throw new LicitacaoEstadoInvalidoException(licitacaoId, lic.Status, "julgamento");
        if (lic.Zona == "ABERTA") throw new PreferenciaMeEppNaoDocumentadaException(licitacaoId);

        var finais = (await c.QueryAsync<(long Fornecedor, decimal Melhor)>(new CommandDefinition(
            """
            select l.fornecedor_id, min(l.valor) from sigov.compras_lance l
            where l.tenant_id=@T and l.licitacao_id=@L and l.ativo=true and not l.is_deleted
            group by l.fornecedor_id order by 2, 1
            """, new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct))).ToList();
        if (finais.Count == 0) throw new JulgamentoSemLancesException(licitacaoId);

        var empate = finais.Select((f, i) => (f, i)).Any(t => t.i > 0 && t.f.Melhor == finais[t.i - 1].Melhor);
        if (empate && string.IsNullOrWhiteSpace(justificativaEmpate))
            throw new JulgamentoEmpateSemJustificativaException();

        var user = UsuarioObrigatorio(x);
        int ordem = 0;
        var itens = new List<ClassificacaoItemDto>();
        foreach (var f in finais)
        {
            ordem++;
            var jd = empate ? $"EMPATE_DECIDIDO: {justificativaEmpate}" : null;
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_proposta(tenant_id,entidade_id,licitacao_id,fornecedor_id,valor,status,ordem_classificacao,justificativa_desclassificacao,created_at,updated_at,created_by,updated_by)
                values(@T,@E,@L,@F,@V,'CLASSIFICADA',@Ord,@Jd,now(),now(),@U,@U)
                """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, F = f.Fornecedor, V = f.Melhor, Ord = ordem, Jd = jd, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
            itens.Add(new ClassificacaoItemDto(ordem, f.Fornecedor, f.Melhor, "CLASSIFICADA"));
        }
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_licitacao set status='JULGADA', updated_at=now() where id=@L and tenant_id=@T",
            new { L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 julgamento {L}: {N} fornecedores classificados (empate={Emp})", licitacaoId, finais.Count, empate);
        return new ClassificacaoDto(itens, empate ? justificativaEmpate : null);
    }

    // ------------------------ REJEICAO DE PROPOSTA (BR-8.4) ---------------------------
    public async Task<PropostaIntegradaDto> PropostaRejeitarAsync(ComprasIntegradasContext x, long licitacaoId, long propostaId, string motivo, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_proposta").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("motivo da rejeição (inexequibilidade) é obrigatório.");
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lic = await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);
        if (lic.Status == "ABERTA") throw new PropostaRejeicaoSemJulgamentoException(licitacaoId);
        if (lic.Status is ("ADJUDICADA" or "HOMOLOGADA")) throw new PropostaRejeicaoPosAdjudicacaoException(licitacaoId);
        // tuple nomeado (mesmo padrao de ContextoEntidadeAsync): o materializer do Dapper por
        // construtor nao aceita o record PropostaIntegradaDto publico (mesma classe de falha do
        // DemRow, defeito 8); ordem_classificacao pode ser NULL (coalesce 0 = sem classificacao).
        var p = await c.QueryFirstOrDefaultAsync<(long Id, long FornecedorId, decimal Valor, int OrdemClassificacao, string Status)>(new CommandDefinition(
            """
            select id, fornecedor_id, valor, coalesce(ordem_classificacao,0), status
            from sigov.compras_proposta where id=@P and licitacao_id=@L and tenant_id=@T
            """, new { P = propostaId, L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (p.Id <= 0) throw new PropostaInexistenteException(propostaId);
        if (p.Status != "CLASSIFICADA") throw new PropostaJaRejeitadaException(propostaId);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_proposta set status='REJEITADA_INEXEQUIVEL', justificativa_desclassificacao=@M, updated_at=now(), updated_by=@U where id=@P and tenant_id=@T",
            new { M = motivo.Trim(), U = UsuarioObrigatorio(x), P = propostaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new PropostaIntegradaDto(p.Id, p.FornecedorId, p.Valor, p.OrdemClassificacao == 0 ? null : p.OrdemClassificacao, "REJEITADA_INEXEQUIVEL");
    }

    // --------------------------- ADJUDICACAO (BR-8.4) ---------------------------------
    public async Task<AdjudicacaoDto> AdjudicarAsync(ComprasIntegradasContext x, long licitacaoId, AdjudicarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_proposta", "compras_adjudicacao", "compras_adjudicacao_item", "compras_licitacao_item", "compras_fornecedor").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var lic = await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);

        var ja = await c.ExecuteScalarAsync<long?>(new CommandDefinition(
            "select id from sigov.compras_adjudicacao where tenant_id=@T and licitacao_id=@L limit 1",
            new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (ja.HasValue) throw new AdjudicacaoJaRealizadaException(licitacaoId);

        // itens adjudicados espelham os itens da licitacao (valor unitario x quantidade maxima)
        var itens = (await c.QueryAsync<(long Id, string Descricao, decimal Qtd, decimal Vu)>(new CommandDefinition(
            "select id, descricao, quantidade, coalesce(valor_estimado,0) from sigov.compras_licitacao_item where tenant_id=@T and licitacao_id=@L order by numero",
            new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct))).ToList();
        if (itens.Count == 0) throw new AdjudicacaoSemItensLicitadosException(licitacaoId);
        var totalEstimado = itens.Sum(i => i.Qtd * i.Vu);

        (long ProId, long Forn, decimal Valor) vencedor;
        if (lic.Status == "JULGADA")
        {
            var classif = await c.QueryFirstOrDefaultAsync<(long ProId, long Forn, decimal Valor)>(new CommandDefinition(
                """
                select id, fornecedor_id, valor from sigov.compras_proposta
                where tenant_id=@T and licitacao_id=@L and status not in ('REJEITADA_INEXEQUIVEL')
                order by ordem_classificacao limit 1
                """, new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            vencedor = classif;
            if (vencedor.ProId <= 0) throw new AdjudicacaoSemPropostaClassificadaException(licitacaoId);
        }
        else if (ModalidadesDiretas.Contains(lic.Modalidade) && lic.Status == "ABERTA")
        {
            // modalidade direta (dispensa/inexigibilidade/consulta previa): sem sessao de lances,
            // o ato seleciona o fornecedor e o valor deriva dos itens da licitacao
            var fornId = r.FornecedorId ?? throw new AdjudicacaoDiretaSemFornecedorException();
            var forn = await c.ExecuteScalarAsync<int>(new CommandDefinition(
                "select count(1) from sigov.compras_fornecedor where id=@F and tenant_id=@T",
                new { F = fornId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (forn == 0) throw new FornecedorInexistenteException(fornId);
            var valorDerivado = Math.Round(totalEstimado, 2);
            var userAdj = UsuarioObrigatorio(x);
            var existente = await c.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
                "select id from sigov.compras_proposta where tenant_id=@T and licitacao_id=@L and fornecedor_id=@F limit 1",
                new { T = x.TenantId, L = licitacaoId, F = fornId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (existente.HasValue)
            {
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.compras_proposta set valor=@V, status='CLASSIFICADA', ordem_classificacao=1, updated_at=now(), updated_by=@U where id=@I",
                    new { V = valorDerivado, U = userAdj, I = existente.Value }, tx, cancellationToken: ct)).ConfigureAwait(false);
                vencedor = (existente.Value, fornId, valorDerivado);
            }
            else
            {
                vencedor = (await c.ExecuteScalarAsync<long>(new CommandDefinition(
                    """
                    insert into sigov.compras_proposta(tenant_id,entidade_id,licitacao_id,fornecedor_id,valor,status,ordem_classificacao,created_at,updated_at,created_by,updated_by)
                    values(@T,@E,@L,@F,@V,'CLASSIFICADA',1,now(),now(),@U,@U) returning id
                    """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, F = fornId, V = valorDerivado, U = userAdj }, tx, cancellationToken: ct)).ConfigureAwait(false), fornId, valorDerivado);
            }
        }
        else
        {
            throw new AdjudicacaoSemJulgamentoException(licitacaoId);
        }

        // distribui o valor da proposta pelos itens (proportional ao estimado; ajuste de arredondamento no ultimo)
        var valorTotal = Math.Round(vencedor.Valor, 2);
        var linhas = new List<(decimal Vu, decimal Tot)>();
        decimal somatoria = 0m;
        for (int i = 0; i < itens.Count; i++)
        {
            var it = itens[i];
            decimal peso = totalEstimado > 0 ? (it.Qtd * it.Vu) / totalEstimado : 1m / itens.Count;
            var vu = Math.Round(valorTotal * peso / it.Qtd, 2);
            var tot = i == itens.Count - 1 ? Math.Round(valorTotal - somatoria, 2) : Math.Round(vu * it.Qtd, 2);
            somatoria = Math.Round(somatoria + tot, 2);
            linhas.Add((vu, tot));
        }

        var user = UsuarioObrigatorio(x);
        var corr = GuidCorr(x.CorrelationId);
        var adjId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_adjudicacao(tenant_id,entidade_id,licitacao_id,proposta_vencedora_id,responsavel_id,adjudicado_em,valor_total,created_at,created_by)
            values(@T,@E,@L,@P,@U,now(),@Vt,now(),@U) returning id
            """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, P = vencedor.ProId, U = user, Vt = valorTotal }, tx, cancellationToken: ct)).ConfigureAwait(false);
        int seq = 0;
        for (int i = 0; i < itens.Count; i++)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_adjudicacao_item(tenant_id,entidade_id,adjudicacao_id,licitacao_item_id,sequencia,descricao,quantidade,valor_unitario,total,correlation_id,created_at,created_by)
                values(@T,@E,@Adj,@Li,@Seq,@Desc,@Qtd,@Vu,@Tot,@Corr,now(),@U)
                """, new { T = x.TenantId, E = x.EntidadeId, Adj = adjId, Li = itens[i].Id, Seq = ++seq, Desc = itens[i].Descricao, Qtd = itens[i].Qtd, Vu = linhas[i].Vu, Tot = linhas[i].Tot, Corr = corr, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_proposta set status='VENCEDORA', updated_at=now(), updated_by=@U where id=@P and tenant_id=@T",
            new { U = user, P = vencedor.ProId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_licitacao set status='ADJUDICADA', updated_at=now() where id=@L and tenant_id=@T",
            new { L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return new AdjudicacaoDto(adjId, vencedor.ProId, vencedor.Forn, valorTotal, itens.Count);
    }

    // ---------------------------- HOMOLOGACAO (BR-8.4) -------------------------------
    public async Task<long> HomologarAsync(ComprasIntegradasContext x, long licitacaoId, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_licitacao", "compras_adjudicacao", "compras_homologacao", "juridico_parecer_condicao", "compras_juridico_consulta_vinculo").ConfigureAwait(false);
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);

        // Sprint 10 gate (BR-10.1): parecer condicionado com condicao aberta bloqueia o proximo passo da contratacao.
        var procLic = await c.QueryFirstAsync<long>(new CommandDefinition(
            "select processo_id from sigov.compras_licitacao where id=@L and tenant_id=@T",
            new { L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await VerificarParecerCondicaoAbertaAsync(c, tx, x, procLic, ct).ConfigureAwait(false);

        var jaH = await c.ExecuteScalarAsync<long?>(new CommandDefinition(
            "select id from sigov.compras_homologacao where tenant_id=@T and licitacao_id=@L limit 1",
            new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (jaH.HasValue) throw new HomologacaoJaRealizadaException(licitacaoId);

        var adj = await c.QueryFirstOrDefaultAsync<(long Id, long Responsavel, DateTime AdjudicadoEm)>(new CommandDefinition(
            "select id, responsavel_id, adjudicado_em from sigov.compras_adjudicacao where tenant_id=@T and licitacao_id=@L limit 1",
            new { T = x.TenantId, L = licitacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (adj.Id <= 0) throw new HomologacaoSemAdjudicacaoException(licitacaoId);

        // separacao de funcoes: ato superior distinto do adjudicador
        if (user == adj.Responsavel) throw new HomologadorEEadjudicadorIguaisException(user);
        // prazo parametrico da homologacao contado da adjudicacao
        var prazo = await LireParametroIntAsync(c, tx, x, "HOMOLOGACAO_PRAZO_MAX_DIAS", ct).ConfigureAwait(false);
        var dias = (clock.UtcNow - adj.AdjudicadoEm.ToUniversalTime()).TotalDays;
        if (dias > prazo) throw new HomologacaoPrazoExcedidoException(licitacaoId, dias, (int)prazo);

        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_homologacao(tenant_id,entidade_id,licitacao_id,adjudicacao_id,responsavel_id,homologado_em,created_at,created_by)
            values(@T,@E,@L,@Adj,@U,now(),now(),@U) returning id
            """, new { T = x.TenantId, E = x.EntidadeId, L = licitacaoId, Adj = adj.Id, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_licitacao set status='HOMOLOGADA', updated_at=now() where id=@L and tenant_id=@T",
            new { L = licitacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 homologacao {Id} da licitacao {L}", id, licitacaoId);
        return id;
    }

    // -------------------------- CONTRATO (BR-8.5) ------------------------------------
    public async Task<ContratoGeradoDto> ContratoGerarAsync(ComprasIntegradasContext x, ContratoGerarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_homologacao", "compras_adjudicacao", "compras_adjudicacao_item", "compras_proposta", "compras_licitacao", "compras_contrato", "compras_contrato_item", "obras_obra", "obras_projeto", "obras_contrato_vinculo").ConfigureAwait(false);
        var modo = string.IsNullOrWhiteSpace(r.AssinaturaModo) ? "" : r.AssinaturaModo.Trim().ToUpperInvariant();
        if (modo is not ("GED" or "FISICA"))
            throw new ArgumentException("assinaturaModo inválido - use GED (assinatura eletrônica) ou FISICA.");
        if (r.DataFim <= r.DataEficacia)
            throw new ArgumentException("dataFim deve ser posterior à dataEficacia.");

        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);

        var hom = await c.QueryFirstOrDefaultAsync<(long Id, long Entidade, long Licitacao, long Adjudicacao)>(new CommandDefinition(
            "select id, entidade_id, licitacao_id, adjudicacao_id from sigov.compras_homologacao where id=@H and tenant_id=@T",
            new { H = r.HomologacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (hom.Id <= 0) throw new HomologacaoInexistenteException(r.HomologacaoId);

        var adj = await c.QueryFirstOrDefaultAsync<(long Id, long Proposta, decimal ValorTotal)>(new CommandDefinition(
            "select id, proposta_vencedora_id, coalesce(valor_total,0) from sigov.compras_adjudicacao where id=@A and tenant_id=@T",
            new { A = hom.Adjudicacao, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (adj.Id <= 0) throw new ContratoSemProcedimentoHomologadoException(r.HomologacaoId);

        var jaC = await c.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            "select id from sigov.compras_contrato where tenant_id=@T and homologacao_id=@H and status in ('VIGENTE','PRORROGADO') limit 1",
            new { T = x.TenantId, H = r.HomologacaoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (jaC.HasValue) throw new ContratoJaGeradoParaHomologacaoException(r.HomologacaoId, jaC.Value);

        var vencedorFornecedor = await c.QueryFirstAsync<long>(new CommandDefinition(
            "select fornecedor_id from sigov.compras_proposta where id=@P and tenant_id=@T",
            new { P = adj.Proposta, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var licInfo = await c.QueryFirstAsync<(long Processo, string Objeto)>(new CommandDefinition(
            "select processo_id, objeto from sigov.compras_licitacao where id=@L and tenant_id=@T",
            new { L = hom.Licitacao, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);

        // Sprint 10 gate (BR-10.4/7.8b): contrato de obra sem projeto APROVADO nao se gera (Lei 14.133 art. 185).
        if (r.ObraId is > 0)
        {
            var stObra = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "select status from sigov.obras_obra where id=@O and tenant_id=@T and ativo and not is_deleted",
                new { O = r.ObraId.Value, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (stObra is null) throw new ObraInexistenteException(r.ObraId.Value);
            var nProjAprovado = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                "select count(1) from sigov.obras_projeto where obra_id=@O and tenant_id=@T and ativo and not is_deleted and status='APROVADO'",
                new { O = r.ObraId.Value, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (nProjAprovado == 0) throw new ObraContratoSemProjetoException(r.ObraId.Value);
        }

        // itens espelham os adjudicados; valor total DERIVADO da adjudicacao (nao editavel)
        var itensAdj = (await c.QueryAsync<(long Li, int Seq, string Desc, decimal Qtd, decimal Vu, decimal Tot)>(new CommandDefinition(
            "select licitacao_item_id, sequencia, descricao, quantidade, valor_unitario, total from sigov.compras_adjudicacao_item where adjudicacao_id=@A and tenant_id=@T order by sequencia",
            new { A = adj.Id, T = x.TenantId }, tx, cancellationToken: ct))).ToList();

        var user = UsuarioObrigatorio(x);
        var corr = GuidCorr(x.CorrelationId);
        var numero = string.IsNullOrWhiteSpace(r.Numero) ? "CT-" + clock.DataAtual.Year + "-" + r.HomologacaoId.ToString("D10") : r.Numero.Trim();
        var contratoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_contrato(tenant_id,entidade_id,processo_id,fornecedor_id,numero,objeto,valor_contratado,data_inicio,data_fim,status,gestor,fiscal,homologacao_id,data_eficacia,assinatura_modo,created_at,updated_at,created_by)
            values(@T,@E,@Proc,@Forn,@Num,@Objeto,@Valor,@Inicio,@Fim,'VIGENTE',@Gestor,@Fiscal,@Hom,@Eficacia,@Modo,now(),now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, Proc = licInfo.Processo, Forn = vencedorFornecedor, Num = numero, Objeto = licInfo.Objeto, Valor = Math.Round(adj.ValorTotal, 2), Inicio = r.DataEficacia, Fim = r.DataFim, Gestor = r.Gestor, Fiscal = r.Fiscal, Hom = r.HomologacaoId, Eficacia = r.DataEficacia, Modo = modo, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);

        int seq = 0;
        foreach (var it in itensAdj)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_contrato_item(tenant_id,entidade_id,contrato_id,sequencia,descricao,quantidade_maxima,valor_unitario,total,correlation_id,created_at,created_by)
                values(@T,@E,@C,@Seq,@Desc,@Qtd,@Vu,@Tot,@Corr,now(),@U)
                """, new { T = x.TenantId, E = x.EntidadeId, C = contratoId, Seq = ++seq, Desc = it.Desc, Qtd = it.Qtd, Vu = it.Vu, Tot = it.Tot, Corr = corr, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        // Sprint 10: registro do vinculo contrato x obra no modulo obras (dependencia canônica, sem copia local).
        if (r.ObraId is > 0)
        {
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.obras_contrato_vinculo(tenant_id,entidade_id,obra_id,codigo,descricao,status,dados,created_at,created_by)
                values(@T,@E,@O,'VC-C2S10-'||@C::text,'Vinculo contrato x obra da frente administrativa integrada C2S10 (BR-7.8/BR-10.4)','ATIVO',jsonb_build_object('compras_contrato_id',@C),now(),@U)
                """, new { T = x.TenantId, E = x.EntidadeId, O = r.ObraId.Value, C = contratoId, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 contrato {Id} gerado da homologacao {H} (valor derivado {V})", contratoId, r.HomologacaoId, adj.ValorTotal);
        return new ContratoGeradoDto(contratoId, numero, vencedorFornecedor, Math.Round(adj.ValorTotal, 2), itensAdj.Count, r.DataEficacia, r.DataFim, modo, "VIGENTE");
    }

    public async Task<ContratoExecucaoDto> ContratoExecucaoAsync(ComprasIntegradasContext x, long contratoId, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "compras_recebimento", "compras_recebimento_item", "juridico_processo").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        var contrato = await c.QueryFirstOrDefaultAsync<(decimal Valor, string Status)>(new CommandDefinition(
            "select valor_contratado, status from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = contratoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (contrato.Valor == 0 && contrato.Status is null) throw new ContratoInexistenteException(contratoId);
        var fornecido = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(i.quantidade * coalesce(i.valor_unitario,0)),0)
            from sigov.compras_recebimento r join sigov.compras_recebimento_item i on i.recebimento_id=r.id
            where r.tenant_id=@T and r.contrato_id=@C
            """, new { T = x.TenantId, C = contratoId }, cancellationToken: ct)).ConfigureAwait(false);
        var teto = await LireParametroIntAsync(c, null, x, "CONTRATO_EXECUCAO_TETO_PCT", ct).ConfigureAwait(false);
        var pct = contrato.Valor > 0 ? Math.Round(fornecido / contrato.Valor * 100m, 2) : 0m;
        // Sprint 10 (BR-10.2): marcador JURIDICO derivado - contencioso ativo com origem CONTRATO neste contrato.
        var contAtivos = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_processo where tenant_id=@T and ativo and not is_deleted and origem_tipo='CONTRATO' and origem_id=@C and encerrado_em is null",
            new { T = x.TenantId, C = contratoId }, cancellationToken: ct)).ConfigureAwait(false);
        return new ContratoExecucaoDto(contratoId, contrato.Valor, fornecido, pct, (int)teto, pct >= teto, contAtivos > 0);
    }

    public async Task ContratoProrrogarAsync(ComprasIntegradasContext x, long contratoId, DateOnly novoFim, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "compras_contrato_historico", "juridico_processo").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("justificativa do ato de prorrogação é obrigatória.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var ctr = await c.QueryFirstOrDefaultAsync<(string Status, DateOnly Inicio, DateOnly Fim)>(new CommandDefinition(
            "select status, data_inicio, data_fim from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = contratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (ctr.Status is null) throw new ContratoInexistenteException(contratoId);
        if (ctr.Status != "VIGENTE") throw new ContratoNaoVigenteException(contratoId, ctr.Status);
        if (novoFim <= ctr.Fim) throw new ArgumentException("novo fim deve ser posterior à vigência atual.");

        // Sprint 10 gate (BR-10.2): contencioso ativo com origem CONTRATO bloqueia a prorrogação ate desfecho registrado.
        var contAtivos = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_processo where tenant_id=@T and ativo and not is_deleted and origem_tipo='CONTRATO' and origem_id=@C and encerrado_em is null",
            new { T = x.TenantId, C = contratoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (contAtivos > 0) throw new ProrrogacaoBloqueadaContenciosoAtivoException(contratoId, (int)contAtivos);

        var tetoAnos = await LireParametroIntAsync(c, tx, x, "PRORROGACAO_TETO_ANOS", ct).ConfigureAwait(false);
        var limite = ctr.Inicio.AddYears((int)tetoAnos);
        if (novoFim > limite) throw new ProrrogacaoExcedeTetoArt106Exception(contratoId, novoFim, limite);
        var maxVezes = await LireParametroIntAsync(c, tx, x, "PRORROGACAO_MAX_VEZES", ct).ConfigureAwait(false);
        var vezes = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.compras_contrato_historico where tenant_id=@T and contrato_id=@C and status_novo='PRORROGACAO'",
            new { T = x.TenantId, C = contratoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (vezes >= maxVezes) throw new ProrrogacoesMaximasExcedidasException(contratoId, vezes, (int)maxVezes);

        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_contrato_historico(tenant_id,entidade_id,contrato_id,status_anterior,status_novo,justificativa,usuario_id,correlation_id,ocorrido_em)
            values(@T,@E,@C,'VIGENTE','PRORROGACAO',@J,@U,@Corr,now())
            """, new { T = x.TenantId, E = x.EntidadeId, C = contratoId, J = $"PRORROGACAO ate {novoFim.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}: {justificativa.Trim()}", U = user, Corr = x.CorrelationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_contrato set data_fim=@F, updated_at=now(), updated_by=@U where id=@C and tenant_id=@T",
            new { F = novoFim, U = user, C = contratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task ContratoRescindirAsync(ComprasIntegradasContext x, long contratoId, string tipo, string justificativa, string? notificacaoRef, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "compras_contrato_historico").ConfigureAwait(false);
        var t = string.IsNullOrWhiteSpace(tipo) ? "" : tipo.Trim().ToUpperInvariant();
        if (t is not ("UNILATERAL_INADIMPLEMENTO" or "BILATERAL_COMUM_ACORDO"))
            throw new ArgumentException("tipo inválido - use UNILATERAL_INADIMPLEMENTO ou BILATERAL_COMUM_ACORDO.");
        if (string.IsNullOrWhiteSpace(justificativa))
            throw new RescisaoSemAtoJustificativaException("rescisão sem justificativa do ato (art. 127 Lei 14.133).");
        if (t == "UNILATERAL_INADIMPLEMENTO" && string.IsNullOrWhiteSpace(notificacaoRef))
            throw new RescisaoSemAtoJustificativaException("rescisão unilateral exige referência da notificação prévia ao contratado (art. 129 Lei 14.133).");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var statusC = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select status from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = contratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (statusC is null) throw new ContratoInexistenteException(contratoId);
        if (statusC != "VIGENTE") throw new ContratoNaoVigenteException(contratoId, statusC);

        var jd = $"{t}: {justificativa.Trim()}" + (string.IsNullOrWhiteSpace(notificacaoRef) ? "" : $" | notificacao {notificacaoRef.Trim()}");
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_contrato_historico(tenant_id,entidade_id,contrato_id,status_anterior,status_novo,justificativa,usuario_id,correlation_id,ocorrido_em)
            values(@T,@E,@C,'VIGENTE','RESCINDIDO',@J,@U,@Corr,now())
            """, new { T = x.TenantId, E = x.EntidadeId, C = contratoId, J = jd, U = user, Corr = x.CorrelationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_contrato set status='RESCIDO', updated_at=now(), updated_by=@U where id=@C and tenant_id=@T",
            new { U = user, C = contratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 contrato {C} rescindido ({T})", contratoId, t);
    }

    // ============================ SPRINT 9 - BR-7.5 A BR-7.7 =========================
    // Fornecimento -> recebimento parcial -> fatura -> destinacao estoque/patrimonio ->
    // obrigacao financeira unica. Invariantes do contrato: recebido <= fornecido; rejeicao
    // sem desfecho nao fecha o fornecimento; sem NF-e nao liquida; 1 fornecimento aprovado =
    // 1 obrigacao (idempotente por referencia); cada destino = 1 movimento referenciado unico.

    static DateOnly AdicionarDiasUteis(DateOnly d, int n)
    {
        var r = d;
        for (var i = 0; i < n; i++)
        {
            r = r.AddDays(1);
            while (r.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) r = r.AddDays(1);
        }
        return r;
    }

    static async Task<string> RecomputarStatusFornecimentoAsync(NpgsqlConnection c, NpgsqlTransaction tx, long tenantId, long fornecimentoId, CancellationToken ct)
    {
        var f = await c.QuerySingleAsync<(decimal Qtd, decimal Recebido)>(new CommandDefinition(
            """
            select f.quantidade, coalesce((select sum(r2.quantidade_aprovada + r2.quantidade_rejeitada) from sigov.compras_fornecimento_recebimento r2 where r2.fornecimento_id=f.id and r2.ativo and not r2.is_deleted),0)
            from sigov.compras_fornecimento f where f.id=@F and f.tenant_id=@T
            """, new { F = fornecimentoId, T = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (f.Recebido < f.Qtd) return f.Recebido > 0 ? "PARCIALMENTE_RECEBIDO" : "FORNECIDO";
        var semDesfecho = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(1) from sigov.compras_fornecimento_recebimento where fornecimento_id=@F and tenant_id=@T and quantidade_rejeitada>0 and desfecho is null and ativo and not is_deleted",
            new { F = fornecimentoId, T = tenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return semDesfecho == 0 ? "CONCLUIDO" : "PARCIALMENTE_RECEBIDO";
    }

    public async Task<long> FornecimentoCriarAsync(ComprasIntegradasContext x, FornecimentoCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_fornecimento", "compras_contrato", "compras_contrato_item", "compras_recebimento", "compras_recebimento_item").ConfigureAwait(false);
        if (r.Quantidade <= 0) throw new ArgumentException("quantidade do fornecimento deve ser maior que zero.");
        var user = UsuarioObrigatorio(x);
        var chave = string.IsNullOrWhiteSpace(r.ChaveIdempotencia) ? null : r.ChaveIdempotencia.Trim();
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);

        var contrato = await c.QueryFirstOrDefaultAsync<(string Status, long FornecedorId, decimal ValorContratado)>(new CommandDefinition(
            "select status, fornecedor_id, valor_contratado from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = r.ContratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (contrato.Status is null) throw new ContratoInexistenteException(r.ContratoId);
        if (contrato.Status != "VIGENTE") throw new ContratoNaoVigenteException(r.ContratoId, contrato.Status);

        var item = await c.QueryFirstOrDefaultAsync<(decimal QtdMax, decimal Vu, string Descricao)>(new CommandDefinition(
            "select quantidade_maxima, valor_unitario, descricao from sigov.compras_contrato_item where id=@I and contrato_id=@C and tenant_id=@T",
            new { I = r.ContratoItemId, C = r.ContratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (item.Descricao is null) throw new ContratoItemInexistenteException(r.ContratoItemId);

        if (chave is not null)
        {
            var exist = await c.QueryFirstOrDefaultAsync<(long Id, decimal Qtd, decimal Vu)>(new CommandDefinition(
                "select id, quantidade, valor_unitario from sigov.compras_fornecimento where tenant_id=@T and chave_idempotencia=@K and ativo and not is_deleted",
                new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (exist.Id > 0)
            {
                if (exist.Qtd == r.Quantidade && exist.Vu == item.Vu) { await tx.CommitAsync(ct).ConfigureAwait(false); return exist.Id; }
                throw new FornecimentoChaveIdempotenciaConflitoException(chave, exist.Id);
            }
        }

        var fornecidoContrato = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(i.quantidade * coalesce(i.valor_unitario,0)),0)
            from sigov.compras_recebimento rc join sigov.compras_recebimento_item i on i.recebimento_id=rc.id
            where rc.tenant_id=@T and rc.contrato_id=@C
            """, new { T = x.TenantId, C = r.ContratoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var pct = contrato.ValorContratado > 0 ? Math.Round(fornecidoContrato / contrato.ValorContratado * 100m, 2) : 0m;
        var teto = await LireParametroIntAsync(c, tx, x, "CONTRATO_EXECUCAO_TETO_PCT", ct).ConfigureAwait(false);
        if (pct >= teto) throw new FornecimentoExcedeExecucaoDisponivelException(r.ContratoId, pct, (int)teto);

        var fornecidoItem = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(quantidade),0) from sigov.compras_fornecimento where tenant_id=@T and contrato_item_id=@I and ativo and not is_deleted",
            new { T = x.TenantId, I = r.ContratoItemId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var disponivel = item.QtdMax - fornecidoItem;
        if (r.Quantidade > disponivel) throw new FornecimentoExcedeItemContratoException(r.ContratoItemId, r.Quantidade, disponivel);

        var corr = GuidCorr(x.CorrelationId);
        var valorTotal = Math.Round(r.Quantidade * item.Vu, 2);
        long id;
        try
        {
            id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.compras_fornecimento(tenant_id,entidade_id,contrato_id,contrato_item_id,fornecedor_id,quantidade,valor_unitario,valor_total,status,chave_idempotencia,correlation_id,created_at,created_by,updated_at)
                values(@T,@E,@C,@I,@F,@Q,@Vu,@Tot,'FORNECIDO',@K,@Corr,now(),@U,now())
                returning id
                """, new { T = x.TenantId, E = x.EntidadeId, C = r.ContratoId, I = r.ContratoItemId, F = contrato.FornecedorId, Q = r.Quantidade, Vu = item.Vu, Tot = valorTotal, K = chave, Corr = corr, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Concorrencia sobre a chave idempotente (ux_compras_fornecimento_chave): dois clientes
            // simultaneos na mesma chave — o vencedor commita primeiro e o perdedor relê o efeito
            // unico dentro da transacao (mesmo conteudo -> mesmo id; divergente -> 409 nomeada).
            // Sem 500 anonimo na race (regra 13; BR-11.2 cenarios transversais).
            var exist2 = await c.QueryFirstOrDefaultAsync<(long Id, decimal Qtd, decimal Vu)>(new CommandDefinition(
                "select id, quantidade, valor_unitario from sigov.compras_fornecimento where tenant_id=@T and chave_idempotencia=@K and ativo and not is_deleted",
                new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (exist2.Id > 0 && exist2.Qtd == r.Quantidade && exist2.Vu == item.Vu) return exist2.Id;
            throw new FornecimentoChaveIdempotenciaConflitoException(chave!, exist2.Id);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 fornecimento {Id} contra contrato {C} item {I} ({Q} x {Vu})", id, r.ContratoId, r.ContratoItemId, r.Quantidade, item.Vu);
        return id;
    }

    public async Task<FornecimentoIntegradoDto> FornecimentoAsync(ComprasIntegradasContext x, long id, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_fornecimento").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        var f = await c.QueryFirstOrDefaultAsync<(long Id, long ContratoId, long ItemId, long FornecedorId, decimal Qtd, decimal Vu, decimal Tot, string Status)>(new CommandDefinition(
            "select id, contrato_id, contrato_item_id, fornecedor_id, quantidade, valor_unitario, valor_total, status from sigov.compras_fornecimento where id=@F and tenant_id=@T",
            new { F = id, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (f.Status is null) throw new FornecimentoInexistenteException(id);
        return new FornecimentoIntegradoDto(f.Id, f.ContratoId, f.ItemId, f.FornecedorId, f.Qtd, f.Vu, f.Tot, f.Status);
    }

    public async Task<RecebimentoIntegradoDto> RecebimentoIntegradoRegistrarAsync(ComprasIntegradasContext x, long fornecimentoId, RecebimentoIntegradoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_fornecimento", "compras_fornecimento_recebimento", "compras_contrato_item", "compras_recebimento", "compras_recebimento_item").ConfigureAwait(false);
        if (r.QuantidadeAprovada < 0 || r.QuantidadeRejeitada < 0 || r.QuantidadeAprovada + r.QuantidadeRejeitada <= 0)
            throw new ArgumentException("aprovada/rejeitada devem ser nao-negativas e soma > 0.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);

        var f = await c.QueryFirstOrDefaultAsync<(long ContratoId, long ItemId, decimal QtdForn, string Status, decimal Vu)>(new CommandDefinition(
            """
            select f.contrato_id, f.contrato_item_id, f.quantidade, f.status, i.valor_unitario
            from sigov.compras_fornecimento f join sigov.compras_contrato_item i on i.id=f.contrato_item_id
            where f.id=@F and f.tenant_id=@T
            """, new { F = fornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (f.Status is null) throw new FornecimentoInexistenteException(fornecimentoId);

        var recebido = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(quantidade_aprovada + quantidade_rejeitada),0) from sigov.compras_fornecimento_recebimento where fornecimento_id=@F and tenant_id=@T and ativo and not is_deleted",
            new { F = fornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var novo = recebido + r.QuantidadeAprovada + r.QuantidadeRejeitada;
        if (novo > f.QtdForn) throw new RecebimentoAcimaFornecidoException(novo, f.QtdForn);
        if (f.Status == "CONCLUIDO") throw new FornecimentoEncerradoParaRecebimentoException(fornecimentoId);
        if (r.QuantidadeRejeitada > 0 && string.IsNullOrWhiteSpace(r.MotivoRejeicao))
            throw new RecebimentoRejeicaoSemMotivoException();

        var prazo = (DateOnly?)null;
        if (r.QuantidadeRejeitada > 0)
        {
            var diasUteis = await LireParametroIntAsync(c, tx, x, "RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS", ct).ConfigureAwait(false);
            prazo = AdicionarDiasUteis(r.DataRecebimento, (int)diasUteis);
        }
        var st = r.QuantidadeAprovada > 0 && r.QuantidadeRejeitada == 0 ? "APROVADO" : r.QuantidadeRejeitada > 0 && r.QuantidadeAprovada == 0 ? "REJEITADO" : "PARCIAL";
        var corr = GuidCorr(x.CorrelationId);
        var recId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_fornecimento_recebimento(tenant_id,entidade_id,fornecimento_id,quantidade_aprovada,quantidade_rejeitada,status,motivo_rejeicao,data_prazo_substituicao,data_recebimento,correlation_id,created_at,created_by,updated_at)
            values(@T,@E,@F,@A,@R,@St,@M,@Pz,@Data,@Corr,now(),@U,now())
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, F = fornecimentoId, A = r.QuantidadeAprovada, R = r.QuantidadeRejeitada, St = st, M = (string?)(r.QuantidadeRejeitada > 0 ? r.MotivoRejeicao?.Trim() : null), Pz = prazo, Data = r.DataRecebimento, Corr = corr, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);

        var legacyId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_recebimento(tenant_id,entidade_id,processo_id,contrato_id,documento,data_recebimento,status,fornecimento_id,created_at,created_by)
            values(@T,@E,null,@C,'REC-FOR-'||@R::text,@Data,@St,@F,now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, C = f.ContratoId, R = recId, Data = r.DataRecebimento, St = st, F = fornecimentoId, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (r.QuantidadeAprovada > 0)
            await c.ExecuteAsync(new CommandDefinition(
                "insert into sigov.compras_recebimento_item(tenant_id,entidade_id,recebimento_id,processo_item_id,quantidade,valor_unitario,tipo_material) values(@T,@E,@Rc,null,@A,@Vu,'CONSUMO')",
                new { T = x.TenantId, E = x.EntidadeId, Rc = legacyId, A = r.QuantidadeAprovada, Vu = f.Vu }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_fornecimento_recebimento set legacy_recebimento_id=@L where id=@R and tenant_id=@T",
            new { L = legacyId, R = recId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var novoStatus = await RecomputarStatusFornecimentoAsync(c, tx, x.TenantId, fornecimentoId, ct).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_fornecimento set status=@S, updated_at=now(), updated_by=@U where id=@F and tenant_id=@T",
            new { S = novoStatus, U = user, F = fornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 recebimento {Rc} do fornecimento {F}: {A} aprov / {Rj} rejeitado -> fornecimento {St}", recId, fornecimentoId, r.QuantidadeAprovada, r.QuantidadeRejeitada, novoStatus);
        return new RecebimentoIntegradoDto(recId, fornecimentoId, r.QuantidadeAprovada, r.QuantidadeRejeitada, st, prazo, null);
    }

    public async Task<RecebimentoIntegradoDto> RecebimentoDesfechoAsync(ComprasIntegradasContext x, long recebimentoId, RecebimentoDesfechoRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_fornecimento_recebimento", "compras_fornecimento").ConfigureAwait(false);
        var df = string.IsNullOrWhiteSpace(r.Desfecho) ? "" : r.Desfecho.Trim().ToUpperInvariant();
        if (df is not ("SUBSTITUIDO" or "ACATADO")) throw new RejecaoDesfechoInvalidoException(r.Desfecho ?? "");
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new ArgumentException("justificativa do desfecho é obrigatória.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rc = await c.QueryFirstOrDefaultAsync<(long FornecimentoId, decimal Rej, string Desfecho, DateOnly? Prazo, string Status)>(new CommandDefinition(
            "select fornecimento_id, quantidade_rejeitada, desfecho, data_prazo_substituicao, status from sigov.compras_fornecimento_recebimento where id=@R and tenant_id=@T",
            new { R = recebimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (rc.Desfecho is null && rc.Rej == 0 && rc.Prazo is null && rc.Status is null) throw new RecebimentoIntegradoInexistenteException(recebimentoId);
        if (rc.Rej <= 0) throw new ArgumentException("recebimento sem rejeição não possui desfecho a registrar.");
        if (rc.Desfecho is not null) throw new RejecaoDesfechoJaRegistradoException(recebimentoId);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_fornecimento_recebimento set desfecho=@D, desfecho_justificativa=@J, updated_at=now(), updated_by=@U where id=@R and tenant_id=@T",
            new { D = df, J = r.Justificativa.Trim(), U = user, R = recebimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var novoStatus = await RecomputarStatusFornecimentoAsync(c, tx, x.TenantId, rc.FornecimentoId, ct).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_fornecimento set status=@S, updated_at=now(), updated_by=@U where id=@F and tenant_id=@T",
            new { S = novoStatus, U = user, F = rc.FornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 desfecho {D} na rejeicao do recebimento {Rc} (fornecimento {F} -> {St})", df, recebimentoId, rc.FornecimentoId, novoStatus);
        var dt = await FornecimentoIntegradoRecebimentoDtoAsync(c, x, recebimentoId, ct).ConfigureAwait(false);
        return dt with { Desfecho = df };
    }

    static async Task<RecebimentoIntegradoDto> FornecimentoIntegradoRecebimentoDtoAsync(NpgsqlConnection c, ComprasIntegradasContext x, long recebimentoId, CancellationToken ct)
    {
        var dt = await c.QueryFirstAsync<(long Id, long F, decimal A, decimal Rj, string St, DateOnly? Pz, string? D)>(new CommandDefinition(
            "select id, fornecimento_id, quantidade_aprovada, quantidade_rejeitada, status, data_prazo_substituicao, desfecho from sigov.compras_fornecimento_recebimento where id=@R and tenant_id=@T",
            new { R = recebimentoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        return new RecebimentoIntegradoDto(dt.Id, dt.F, dt.A, dt.Rj, dt.St, dt.Pz, dt.D);
    }

    public async Task<FaturaIntegradaDto> FaturaVincularAsync(ComprasIntegradasContext x, long fornecimentoId, FaturaVincularRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_fatura_integrada", "compras_fornecimento", "compras_fornecedor").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(r.Numero)) throw new ArgumentException("numero da fatura/NF-e é obrigatório.");
        var natureza = string.IsNullOrWhiteSpace(r.Natureza) ? "" : r.Natureza.Trim().ToUpperInvariant().Replace("-", "_");
        if (natureza is not ("NFE" or "NFS_E")) throw new FaturaNaturezaInvalidaException(r.Natureza ?? "");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var f = await c.QueryFirstOrDefaultAsync<(long FornecedorId, decimal ValorTotal, string Cnpj)>(new CommandDefinition(
            """
            select fn.fornecedor_id, fn.valor_total, fp.documento
            from sigov.compras_fornecimento fn join sigov.compras_fornecedor fp on fp.id=fn.fornecedor_id
            where fn.id=@F and fn.tenant_id=@T
            """, new { F = fornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (f.Cnpj is null) throw new FornecimentoInexistenteException(fornecimentoId);
        var num = r.Numero.Trim();
        var cnpj = r.CnpjFornecedor.Trim();
        var exist = await c.QueryFirstOrDefaultAsync<(long Id, string Numero, decimal Valor, string CnpjF)>(new CommandDefinition(
            "select id, numero, valor, cnpj_fornecedor from sigov.compras_fatura_integrada where tenant_id=@T and fornecimento_id=@F and ativo and not is_deleted",
            new { T = x.TenantId, F = fornecimentoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (exist.Id > 0)
        {
            if (exist.Numero == num && exist.Valor == r.Valor && exist.CnpjF == cnpj)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new FaturaIntegradaDto(exist.Id, fornecimentoId, exist.Numero, "NFE", exist.Valor, true);
            }
            throw new FaturaJaVinculadaConflitoException(fornecimentoId, exist.Id, exist.Numero);
        }
        if (!string.Equals(cnpj, f.Cnpj, StringComparison.Ordinal))
            throw new FaturaFornecedorDiscrepanteException(cnpj, f.Cnpj);
        if (Math.Round(r.Valor, 2) != Math.Round(f.ValorTotal, 2))
            throw new FaturaValorDivergenteException(r.Valor, f.ValorTotal);
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_fatura_integrada(tenant_id,entidade_id,fornecimento_id,numero,serie,natureza,cnpj_fornecedor,valor,data_emissao,conferida,correlation_id,created_at,created_by)
            values(@T,@E,@F,@Num,@Ser,@Nat,@Cnpj,@V,@De,true,@Corr,now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, F = fornecimentoId, Num = num, Ser = string.IsNullOrWhiteSpace(r.Serie) ? null : r.Serie.Trim(), Nat = natureza, Cnpj = cnpj, V = Math.Round(r.Valor, 2), De = r.DataEmissao, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 fatura {Id} ({Num}) vinculada e conferida ao fornecimento {F}", id, num, fornecimentoId);
        return new FaturaIntegradaDto(id, fornecimentoId, num, natureza, Math.Round(r.Valor, 2), true);
    }

    public async Task<DestinacaoIntegradaDto> DestinacaoRegistrarAsync(ComprasIntegradasContext x, DestinacaoIntegradaRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_destinacao_integrada", "compras_fornecimento_recebimento", "compras_fornecimento", "compras_contrato_item", "almoxarifado_local", "almoxarifado_material", "almoxarifado_movimentacao", "patrimonio_bem", "patrimonio_categoria").ConfigureAwait(false);
        var alvo = string.IsNullOrWhiteSpace(r.AlvoTipo) ? "" : r.AlvoTipo.Trim().ToUpperInvariant();
        if (alvo is not ("ESTOQUE" or "PATRIMONIO")) throw new DestinoTipoInvalidoException(r.AlvoTipo ?? "");
        if (r.Quantidade <= 0) throw new ArgumentException("quantidade do destino deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(r.Referencia)) throw new ArgumentException("referencia do movimento referenciado é obrigatória (idempotência por referência).");
        var referencia = r.Referencia.Trim();
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rc = await c.QueryFirstOrDefaultAsync<(long FornecimentoId, decimal Aprovada, decimal Vu, long ContratoId, string St, DateOnly DataRec, string Nf)>(new CommandDefinition(
            """
            select f.id, r2.quantidade_aprovada, i.valor_unitario, f.contrato_id, f.status, r2.data_recebimento, fa.numero
            from sigov.compras_fornecimento_recebimento r2
            join sigov.compras_fornecimento f on f.id=r2.fornecimento_id
            join sigov.compras_contrato_item i on i.id=f.contrato_item_id
            left join sigov.compras_fatura_integrada fa on fa.fornecimento_id=f.id and fa.tenant_id=r2.tenant_id and fa.ativo and not fa.is_deleted
            where r2.id=@R and r2.tenant_id=@T
            """, new { R = r.FornecimentoRecebimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (rc.St is null) throw new RecebimentoIntegradoInexistenteException(r.FornecimentoRecebimentoId);
        var valor = Math.Round(r.Quantidade * rc.Vu, 2);
        // Idempotencia por referencia tem prioridade sobre as regras de negocio: um replay deve
        // ser sem efeito (ou conflitar por conteudo) independentemente do estado atual; a regra
        // de teto (excede qtd aprovada) vale apenas para referencias novas.
        var exist = await c.QueryFirstOrDefaultAsync<(long Id, string Alvo, decimal Qtd, decimal Vl, long Rec)>(new CommandDefinition(
            "select id, alvo_tipo, quantidade, valor, fornecimento_recebimento_id from sigov.compras_destinacao_integrada where tenant_id=@T and referencia=@Ref and ativo and not is_deleted",
            new { T = x.TenantId, Ref = referencia }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (exist.Id > 0)
        {
            if (exist.Alvo == alvo && exist.Qtd == r.Quantidade && exist.Vl == valor && exist.Rec == r.FornecimentoRecebimentoId)
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new DestinacaoIntegradaDto(exist.Id, exist.Rec, exist.Alvo, exist.Qtd, exist.Vl, referencia, null);
            }
            throw new DestinoReferenciaConflitoException(referencia, exist.Id);
        }
        var destinado = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(quantidade),0) from sigov.compras_destinacao_integrada where tenant_id=@T and fornecimento_recebimento_id=@R and ativo and not is_deleted",
            new { T = x.TenantId, R = r.FornecimentoRecebimentoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (r.Quantidade + destinado > rc.Aprovada) throw new DestinoExcedeQtdAprovadaException(r.Quantidade, rc.Aprovada - destinado);

        var destId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_destinacao_integrada(tenant_id,entidade_id,fornecimento_recebimento_id,alvo_tipo,quantidade,valor,referencia,correlation_id,created_at,created_by)
            values(@T,@E,@R,@Alvo,@Q,@V,@Ref,@Corr,now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, R = r.FornecimentoRecebimentoId, Alvo = alvo, Q = r.Quantidade, V = valor, Ref = referencia, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);

        long alvoId;
        if (alvo == "ESTOQUE")
        {
            if (r.LocalId is null || r.MaterialId is null) throw new ArgumentException("destino ESTOQUE exige localId e materialId.");
            var locSt = await c.ExecuteScalarAsync<string>(new CommandDefinition(
                """
                select case
                   when not exists(select 1 from sigov.almoxarifado_local where id=@L and tenant_id=@T) then 'INEXISTENTE'
                   when not exists(select 1 from sigov.almoxarifado_local where id=@L and tenant_id=@T and ativo) then 'INATIVO'
                   else 'ATIVO' end
                """, new { L = r.LocalId.Value, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (locSt == "INEXISTENTE") throw new DestinoLocalInexistenteException(r.LocalId.Value);
            if (locSt == "INATIVO") throw new DestinoLocalInativoException(r.LocalId.Value);
            if (!(await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.almoxarifado_material where id=@M and tenant_id=@T and ativo)", new { M = r.MaterialId.Value, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false)))
                throw new DestinoMaterialInexistenteException(r.MaterialId.Value);
            var saldoAntes = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "select coalesce(greatest(coalesce(sum(case when tipo='ENTRADA' then quantidade else -quantidade end),0),0),0) from sigov.almoxarifado_movimentacao where tenant_id=@T and almoxarifado_id=@L and material_id=@M",
                new { T = x.TenantId, L = r.LocalId.Value, M = r.MaterialId.Value }, tx, cancellationToken: ct)).ConfigureAwait(false);
            alvoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.almoxarifado_movimentacao(tenant_id,entidade_id,almoxarifado_id,material_id,tipo,motivo,quantidade,valor_unitario,documento_origem,saldo_antes,saldo_depois,usuario_id,correlation_id)
                values(@T,@E,@L,@M,'ENTRADA','FORNECIMENTO',@Q,@Vu,'FORN-'||@F||'-REC-'||@R,@SA,@SD,@U,@CorrTxt)
                returning id
                """, new { T = x.TenantId, E = x.EntidadeId, L = r.LocalId.Value, M = r.MaterialId.Value, Q = r.Quantidade, Vu = rc.Vu, F = rc.FornecimentoId, R = r.FornecimentoRecebimentoId, SA = saldoAntes, SD = saldoAntes + r.Quantidade, U = user, CorrTxt = x.CorrelationId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        else
        {
            if (r.CategoriaId is not null && !(await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.patrimonio_categoria where id=@C and tenant_id=@T and ativo and not is_deleted)", new { C = r.CategoriaId.Value, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false)))
                throw new ArgumentException($"categoria de patrimônio {r.CategoriaId.Value} inexistente ou inativa.");
            alvoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                insert into sigov.patrimonio_bem(tenant_id,descricao,codigo_tombo,tipo_bem,categoria_id,localizacao,contrato_id,data_aquisicao,valor_aquisicao,responsavel_usuario_id,nota_fiscal,created_by)
                values(@T,@Desc,'PAT-FOR-'||@Dest::text,@Tipo,@Cat,@Loc,@C,@Data,@Valor,@U,@Nf,@U)
                returning id
                """, new { T = x.TenantId, Desc = referencia, Dest = destId, Tipo = string.IsNullOrWhiteSpace(r.TipoBem) ? "OUTRO" : r.TipoBem.Trim(), Cat = r.CategoriaId, Loc = string.IsNullOrWhiteSpace(r.Localizacao) ? null : r.Localizacao.Trim(), C = rc.ContratoId, Data = rc.DataRec, Valor = valor, U = user, Nf = rc.Nf }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_destinacao_integrada set alvo_id=@A where id=@D and tenant_id=@T",
            new { A = alvoId, D = destId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 destino {D} ({Alvo} {Q} un ref {Ref}) -> alvo {A}", destId, alvo, r.Quantidade, referencia, alvoId);
        return new DestinacaoIntegradaDto(destId, r.FornecimentoRecebimentoId, alvo, r.Quantidade, valor, referencia, alvoId);
    }

    public async Task<ObrigacaoFinanceiraDto> ObrigacaoGerarAsync(ComprasIntegradasContext x, ObrigacaoGerarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_obrigacao", "compras_fornecimento", "compras_fornecimento_recebimento", "compras_contrato_item").ConfigureAwait(false);
        var referencia = string.IsNullOrWhiteSpace(r.ReferenciaIdempotencia) ? "" : r.ReferenciaIdempotencia.Trim();
        if (referencia.Length == 0) throw new ArgumentException("referência de idempotência da obrigação é obrigatória.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var st = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select status from sigov.compras_fornecimento where id=@F and tenant_id=@T",
            new { F = r.FornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (st is null) throw new FornecimentoInexistenteException(r.FornecimentoId);
        if (st != "CONCLUIDO") throw new ObrigacaoFornecimentoIncompletoException(r.FornecimentoId, st);
        var valor = Math.Round(await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(r2.quantidade_aprovada * i.valor_unitario),0)
            from sigov.compras_fornecimento_recebimento r2
            join sigov.compras_fornecimento f on f.id=r2.fornecimento_id
            join sigov.compras_contrato_item i on i.id=f.contrato_item_id
            where r2.fornecimento_id=@F and r2.tenant_id=@T and r2.ativo and not r2.is_deleted
            """, new { F = r.FornecimentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false), 2);
        var exist = await c.QueryFirstOrDefaultAsync<(long Id, string Referencia, decimal Valor, string Status)>(new CommandDefinition(
            """
            select id, referencia_idempotencia, valor_liquidado, status
            from sigov.compras_obrigacao where tenant_id=@T and fornecimento_id=@F and ativo and not is_deleted limit 1
            """, new { T = x.TenantId, F = r.FornecimentoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (exist.Id > 0)
        {
            if (exist.Referencia == referencia && exist.Valor == valor && exist.Status == "REGISTRADA")
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return new ObrigacaoFinanceiraDto(exist.Id, r.FornecimentoId, referencia, exist.Valor, exist.Status);
            }
            throw new ObrigacaoDuplicadaReferenciaException(referencia, exist.Id);
        }
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_obrigacao(tenant_id,entidade_id,fornecimento_id,referencia_idempotencia,valor_liquidado,status,correlation_id,created_at,created_by,updated_at)
            values(@T,@E,@F,@Ref,@V,'REGISTRADA',@Corr,now(),@U,now())
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, F = r.FornecimentoId, Ref = referencia, V = valor, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 obrigacao {Id} gerada para o fornecimento {F} (valor liquidado {V})", id, r.FornecimentoId, valor);
        return new ObrigacaoFinanceiraDto(id, r.FornecimentoId, referencia, valor, "REGISTRADA");
    }

    public async Task<ObrigacaoFinanceiraDto> ObrigacaoLiquidarAsync(ComprasIntegradasContext x, long obrigacaoId, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_obrigacao", "compras_fatura_integrada").ConfigureAwait(false);
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var ob = await c.QueryFirstOrDefaultAsync<(long Id, long FornecimentoId, string Referencia, decimal Valor, string Status)>(new CommandDefinition(
            "select id, fornecimento_id, referencia_idempotencia, valor_liquidado, status from sigov.compras_obrigacao where id=@O and tenant_id=@T",
            new { O = obrigacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (ob.Status is null) throw new ObrigacaoInexistenteException(obrigacaoId);
        if (ob.Status is "LIQUIDADA" or "ESTORNADA") throw new ObrigacaoJaLiquidadaException(obrigacaoId, ob.Status);
        var comFatura = await c.ExecuteScalarAsync<bool>(new CommandDefinition(
            "select exists(select 1 from sigov.compras_fatura_integrada where tenant_id=@T and fornecimento_id=@F and conferida and ativo and not is_deleted)",
            new { T = x.TenantId, F = ob.FornecimentoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (!comFatura) throw new ObrigacaoLiquidacaoSemFaturaException(obrigacaoId);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_obrigacao set status='LIQUIDADA', data_liquidacao=now(), updated_at=now(), updated_by=@U where id=@O and tenant_id=@T",
            new { U = user, O = obrigacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 obrigacao {O} liquidada (fornecimento {F}, valor {V})", obrigacaoId, ob.FornecimentoId, ob.Valor);
        return new ObrigacaoFinanceiraDto(ob.Id, ob.FornecimentoId, ob.Referencia, ob.Valor, "LIQUIDADA");
    }

    public async Task<ObrigacaoFinanceiraDto> ObrigacaoEstornarAsync(ComprasIntegradasContext x, long obrigacaoId, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_obrigacao").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ObrigacaoEstornoSemJustificativaException();
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var ob = await c.QueryFirstOrDefaultAsync<(long Id, long FornecimentoId, string Referencia, decimal Valor, string Status)>(new CommandDefinition(
            "select id, fornecimento_id, referencia_idempotencia, valor_liquidado, status from sigov.compras_obrigacao where id=@O and tenant_id=@T",
            new { O = obrigacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (ob.Status is null) throw new ObrigacaoInexistenteException(obrigacaoId);
        if (ob.Status is not ("REGISTRADA" or "LIQUIDADA")) throw new ObrigacaoJaLiquidadaException(obrigacaoId, ob.Status);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_obrigacao set status='ESTORNADA', justificativa_estorno=@J, updated_at=now(), updated_by=@U where id=@O and tenant_id=@T",
            new { J = justificativa.Trim(), U = user, O = obrigacaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S9 obrigacao {O} estornada por compensacao", obrigacaoId);
        return new ObrigacaoFinanceiraDto(ob.Id, ob.FornecimentoId, ob.Referencia, ob.Valor, "ESTORNADA");
    }

    // ============================ SPRINT 10 - BR-7.8 + BR-10.X =======================
    // Vinculos Juridico x Compras + Convenios/Obras (etapa 3/3 da frente administrativa).
    // Banco = autoridade (regras 11/12): os comandos escrevem nas tabelas canonicas dos modulos
    // legados (juridico_*, convenio_*, obras_*) como DEPENDENCIA; os vinculos cruzados ficam no
    // ledger compras_vinculo_externo + compras_juridico_consulta_vinculo (quebra de vinculo exige
    // justificativa e ato registrado - BR-10.5; nunca delete fisico).

    async Task VerificarParecerCondicaoAbertaAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, long processoId, CancellationToken ct)
    {
        var abertas = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            select count(1)
            from sigov.juridico_parecer_condicao cd
            join sigov.juridico_parecer pr on pr.id = cd.parecer_id and pr.tenant_id = cd.tenant_id and pr.ativo and not pr.is_deleted and pr.status = 'EMITIDO'
            join sigov.compras_juridico_consulta_vinculo v on v.consulta_id = pr.consulta_id and v.tenant_id = pr.tenant_id and v.ativo and not v.is_deleted
            where cd.tenant_id = @T and cd.ativo and not cd.is_deleted and cd.status = 'ABERTA' and v.processo_compras_id = @P
            """, new { T = x.TenantId, P = processoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (abertas > 0) throw new ParecerCondicaoAbertaException(processoId, (int)abertas);
    }

    async Task<ConsultaJuridicaDto> LeituraConsultaDtoAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, long consultaId, CancellationToken ct)
    {
        var r = await c.QuerySingleAsync<(long ConsultaId, long Processo, string Tipo, string Status, string? Conclusao, int Abertas, long? Pai, string Ref)>(new CommandDefinition(
            """
            select cs.id, v.processo_compras_id, v.tipo_consulta, cs.status,
                   (select pr.conclusao from sigov.juridico_parecer pr where pr.consulta_id=cs.id and pr.tenant_id=@T and pr.ativo and not pr.is_deleted and pr.status='EMITIDO' limit 1),
                   (select count(1)::int from sigov.juridico_parecer_condicao cd
                      join sigov.juridico_parecer pr2 on pr2.id=cd.parecer_id and pr2.tenant_id=cd.tenant_id
                     where pr2.consulta_id=cs.id and pr2.ativo and not pr2.is_deleted and pr2.status='EMITIDO' and cd.ativo and not cd.is_deleted and cd.status='ABERTA'),
                   cs.consulta_pai_id, v.referencia_numero
            from sigov.juridico_consulta cs
            join sigov.compras_juridico_consulta_vinculo v on v.consulta_id=cs.id and v.tenant_id=cs.tenant_id and v.ativo and not v.is_deleted
            where cs.id=@I and cs.tenant_id=@T and cs.ativo and not cs.is_deleted
            """, new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new ConsultaJuridicaDto(r.ConsultaId, r.Processo, r.Tipo, r.Status, r.Conclusao, r.Abertas, r.Pai, r.Ref);
    }

    async Task<ContenciosoDto> LeituraContenciosoDtoAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, long processoId, CancellationToken ct)
    {
        var r = await c.QuerySingleAsync<(long Id, string? Ot, long? Oi, string Caso, string Status, string? Resultado, int Mov)>(new CommandDefinition(
            """
            select p.id, p.origem_tipo, p.origem_id, coalesce(p.dados->>'caso','ATIVO'), p.status, p.resultado,
                   (select count(1)::int from sigov.juridico_processo_movimento m where m.processo_id=p.id and m.tenant_id=p.tenant_id)
            from sigov.juridico_processo p
            where p.id=@I and p.tenant_id=@T
            """, new { I = processoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return new ContenciosoDto(r.Id, r.Ot ?? "", r.Oi ?? 0, r.Caso, r.Status, r.Resultado, r.Mov);
    }

    // Execucao do instrumento: repasse RECEBIDO (banco = autoridade) vs etapas executadas
    // (etapa -> meta -> projeto -> instrumento, sem copia local). Saldo = recebido - executado.
    async Task<(decimal Recebido, decimal Executado)> ExecucaoConvenioAsync(NpgsqlConnection c, NpgsqlTransaction tx, ComprasIntegradasContext x, long instrumentoId, CancellationToken ct)
    {
        var recebido = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(valor),0) from sigov.convenio_repasse where instrumento_id=@I and tenant_id=@T and deleted_at is null and status='RECEBIDO'",
            new { I = instrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var executado = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            select coalesce(sum(e.valor_executado),0)
            from sigov.convenio_etapa e
            join sigov.convenio_meta m on m.id = e.meta_id and m.deleted_at is null
            join sigov.convenio_projeto pj on pj.id = m.projeto_id and pj.deleted_at is null
            where pj.instrumento_id=@I and e.tenant_id=@T and e.deleted_at is null and e.status<>'CANCELADA'
            """, new { I = instrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        return (recebido, executado);
    }

    // -------------------------------- CONSULTA JURIDICA (BR-10.1) ---------------------
    public async Task<ConsultaJuridicaDto> ConsultaJuridicaCriarAsync(ComprasIntegradasContext x, ConsultaJuridicaCriarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_processo", "juridico_consulta", "compras_juridico_consulta_vinculo").ConfigureAwait(false);
        var tipo = string.IsNullOrWhiteSpace(r.TipoConsulta) ? "" : r.TipoConsulta.Trim().ToUpperInvariant();
        if (tipo is not ("PARECER" or "IMPUGNACAO" or "DEFESA" or "COBRANCA")) throw new ConsultaTipoInvalidoException(tipo);
        if (r.ProcessoComprasId <= 0) throw new ArgumentException("processoComprasId é obrigatório.");
        if (string.IsNullOrWhiteSpace(r.ReferenciaNumero)) throw new ArgumentException("referenciaNumero do ato administrativo é obrigatória.");
        if (string.IsNullOrWhiteSpace(r.Descricao)) throw new ArgumentException("descricao/assunto da consulta é obrigatório.");
        if (string.IsNullOrWhiteSpace(r.SecretariaSetor)) throw new ArgumentException("secretariaSetor solicitante é obrigatório.");
        var chave = string.IsNullOrWhiteSpace(r.ChaveIdempotencia) ? "" : r.ChaveIdempotencia.Trim();
        if (chave.Length > 150) throw new ArgumentException("chaveIdempotencia excede 150 caracteres.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var numProc = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select numero from sigov.compras_processo where id=@P and tenant_id=@T",
            new { P = r.ProcessoComprasId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (numProc is null) throw new ProcessoComprasInexistenteException(r.ProcessoComprasId);
        if (!string.IsNullOrEmpty(chave))
        {
            var exist = await c.QueryFirstOrDefaultAsync<(long Consulta, long Processo, string Tipo)>(new CommandDefinition(
                "select consulta_id, processo_compras_id, tipo_consulta from sigov.compras_juridico_consulta_vinculo where tenant_id=@T and chave_idempotencia=@K and ativo and not is_deleted limit 1",
                new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (exist.Consulta > 0)
            {
                if (exist.Processo == r.ProcessoComprasId && exist.Tipo == tipo)
                {
                    var replay = await LeituraConsultaDtoAsync(c, tx, x, exist.Consulta, ct).ConfigureAwait(false);
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return replay;
                }
                throw new ConsultaChaveIdempotenciaConflitoException(chave, exist.Consulta);
            }
        }
        var consultaId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.juridico_consulta(tenant_id,entidade_id,codigo,descricao,status,secretaria_setor,solicitante,assunto,data_solicitacao,prazo_resposta,custo_honorario,created_at,created_by)
            values(@T,@E,@Ref,'Consulta juridica '||@Tipo||' vinculada a contratacao (BR-10.1)','ABERTA',@Setor,@Sol,@Assunto,@Data,@Prazo,@Custo,now(),@U)
            returning id
            """, new { T = x.TenantId, E = ent, Ref = r.ReferenciaNumero.Trim(), Tipo = tipo, Setor = r.SecretariaSetor.Trim(), Sol = r.Solicitante, Assunto = r.Descricao.Trim(), Data = r.DataSolicitacao, Prazo = r.PrazoResposta, Custo = Math.Round(r.CustoHonorario, 4), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_juridico_consulta_vinculo(tenant_id,entidade_id,consulta_id,processo_compras_id,tipo_consulta,referencia_numero,chave_idempotencia,correlation_id,created_at,created_by)
            values(@T,@E,@I,@P,@Tipo,@Ref,@K,@Corr,now(),@U)
            """, new { T = x.TenantId, E = x.EntidadeId, I = consultaId, P = r.ProcessoComprasId, Tipo = tipo, Ref = r.ReferenciaNumero.Trim(), K = string.IsNullOrEmpty(chave) ? null : chave, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var dtoNovo = await LeituraConsultaDtoAsync(c, tx, x, consultaId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 consulta juridica {I} aberta ({Tipo} do processo {P})", consultaId, tipo, r.ProcessoComprasId);
        return dtoNovo;
    }

    public async Task<ConsultaJuridicaDto> ParecerEmitirAsync(ComprasIntegradasContext x, long consultaId, ParecerEmitirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "juridico_consulta", "juridico_parecer", "juridico_parecer_condicao").ConfigureAwait(false);
        var conclusao = string.IsNullOrWhiteSpace(r.Conclusao) ? "" : r.Conclusao.Trim().ToUpperInvariant();
        if (conclusao is not ("FAVORECIMENTO" or "IMPROCEDENCIA" or "CONDICIONADO")) throw new ParecerConclusaoInvalidaException(conclusao);
        var condicoes = (r.Condicoes ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (condicoes.Count > 50) throw new ArgumentException("no máximo 50 condicionantes por parecer.");
        if (conclusao == "CONDICIONADO" && condicoes.Count == 0) throw new ParecerCondicionadoSemCondicaoException();
        if (conclusao != "CONDICIONADO" && condicoes.Count > 0) throw new ParecerCondicaoIndevidaException();
        if (string.IsNullOrWhiteSpace(r.Resumo)) throw new ArgumentException("resumo/ementa do parecer é obrigatório.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var st = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select status from sigov.juridico_consulta where id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (st is null) throw new ConsultaJuridicaInexistenteException(consultaId);
        var jaEmitido = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_parecer where consulta_id=@I and tenant_id=@T and ativo and not is_deleted and status='EMITIDO'",
            new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (jaEmitido > 0) throw new ParecerJaEmitidoException(consultaId);
        var parecerId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.juridico_parecer(tenant_id,entidade_id,descricao,status,consulta_id,conclusao,ementa,emitido_em,created_at,created_by)
            values(@T,@E,'Parecer da consulta '||@I::text||' ('||@C||') (BR-10.1)','EMITIDO',@I,@C,@Resumo,now(),now(),@U)
            returning id
            """, new { T = x.TenantId, E = ent, I = consultaId, C = conclusao, Resumo = r.Resumo.Trim(), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        foreach (var cond in condicoes)
        {
            await c.ExecuteAsync(new CommandDefinition(
                "insert into sigov.juridico_parecer_condicao(tenant_id,entidade_id,parecer_id,descricao,status,correlation_id,created_at,created_by) values(@T,@E,@P,@D,'ABERTA',@Corr,now(),@U)",
                new { T = x.TenantId, E = x.EntidadeId, P = parecerId, D = cond, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.juridico_consulta set status=@S, updated_at=now(), updated_by=@U where id=@I and tenant_id=@T",
            new { S = conclusao == "CONDICIONADO" ? "CONDICIONADA" : "RESOLVIDA", U = user, I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var dtoParecer = await LeituraConsultaDtoAsync(c, tx, x, consultaId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 parecer {P} emitido para a consulta {I} ({C}, {N} condicoes)", parecerId, consultaId, conclusao, condicoes.Count);
        return dtoParecer;
    }

    public async Task<CondicaoSatisfeitaDto> CondicaoSatisfazerAsync(ComprasIntegradasContext x, long condicaoId, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "juridico_parecer_condicao", "juridico_parecer", "juridico_consulta").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new SatisfazerCondicaoSemJustificativaException();
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var cd = await c.QueryFirstOrDefaultAsync<(long Id, long Parecer, long Consulta, string Status)>(new CommandDefinition(
            """
            select cd.id, cd.parecer_id, coalesce(pr.consulta_id,0), cd.status
            from sigov.juridico_parecer_condicao cd
            join sigov.juridico_parecer pr on pr.id = cd.parecer_id and pr.tenant_id = cd.tenant_id
            where cd.id = @I and cd.tenant_id = @T and cd.ativo and not cd.is_deleted
            """, new { I = condicaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (cd.Id <= 0) throw new CondicaoParecerInexistenteException(condicaoId);
        if (cd.Status != "ABERTA") throw new CondicaoJaSatisfeitaException(condicaoId);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.juridico_parecer_condicao set status='SATISFEITA', satisfeita_em=now(), justificativa=@J, updated_at=now(), updated_by=@U where id=@I and tenant_id=@T",
            new { J = justificativa.Trim(), U = user, I = condicaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var restantes = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_parecer_condicao where parecer_id=@P and tenant_id=@T and ativo and not is_deleted and status='ABERTA'",
            new { P = cd.Parecer, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        string statusConsulta = "SEM_PARECER";
        if (cd.Consulta > 0)
        {
            if (restantes == 0)
                await c.ExecuteAsync(new CommandDefinition(
                    "update sigov.juridico_consulta set status='RESOLVIDA', updated_at=now(), updated_by=@U where id=@C and tenant_id=@T and status='CONDICIONADA'",
                    new { U = user, C = cd.Consulta, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            statusConsulta = await c.QueryFirstAsync<string>(new CommandDefinition(
                "select status from sigov.juridico_consulta where id=@C and tenant_id=@T",
                new { C = cd.Consulta, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 condicao {I} satisfeita (restam {R} na consulta {C})", condicaoId, restantes, cd.Consulta);
        return new CondicaoSatisfeitaDto(condicaoId, cd.Consulta, (int)restantes, statusConsulta);
    }

    public async Task<ConsultaJuridicaDto> ConsultaFilialAsync(ComprasIntegradasContext x, long consultaId, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "juridico_consulta", "compras_juridico_consulta_vinculo").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("justificativa do recurso/filial é obrigatória.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var pai = await c.QueryFirstOrDefaultAsync<(string Status, string Setor, string? Sol, string Assunto)>(new CommandDefinition(
            "select status, secretaria_setor, solicitante, assunto from sigov.juridico_consulta where id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (pai.Status is null) throw new ConsultaJuridicaInexistenteException(consultaId);
        if (pai.Status != "RESOLVIDA") throw new ConsultaPaiNaoResolvidaException(consultaId, pai.Status);
        var jaFilial = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_consulta where consulta_pai_id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (jaFilial > 0) throw new FilialDuplicadaException(consultaId);
        var linkPai = await c.QueryFirstOrDefaultAsync<(long Processo, string Tipo, string Ref)>(new CommandDefinition(
            "select processo_compras_id, tipo_consulta, referencia_numero from sigov.compras_juridico_consulta_vinculo where consulta_id=@I and tenant_id=@T and ativo and not is_deleted limit 1",
            new { I = consultaId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (linkPai.Processo <= 0) throw new ConsultaSemProcessoException(consultaId);
        var filialId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.juridico_consulta(tenant_id,entidade_id,descricao,status,secretaria_setor,solicitante,assunto,data_solicitacao,custo_honorario,consulta_pai_id,justificativa,created_at,created_by)
            values(@T,@E,'Filial (recurso) da consulta '||@I::text||': '||@J,'ABERTA',@Setor,@Sol,'FILIAL: '||@Assunto,@Data,0,@I,@J,now(),@U)
            returning id
            """, new { T = x.TenantId, E = ent, I = consultaId, J = justificativa.Trim(), Setor = pai.Setor, Sol = pai.Sol, Assunto = pai.Assunto, Data = clock.DataAtual, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.compras_juridico_consulta_vinculo(tenant_id,entidade_id,consulta_id,processo_compras_id,tipo_consulta,referencia_numero,correlation_id,created_at,created_by)
            values(@T,@E,@FI,@P,@Tipo,@Ref||'-FILIAL',@Corr,now(),@U)
            """, new { T = x.TenantId, E = x.EntidadeId, FI = filialId, P = linkPai.Processo, Tipo = linkPai.Tipo, Ref = linkPai.Ref, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var dtoFilial = await LeituraConsultaDtoAsync(c, tx, x, filialId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 consulta filial {F} aberta contra a original {I} (original preservada)", filialId, consultaId);
        return dtoFilial;
    }

    // ------------------------------- CONTENCIOSO (BR-10.2) ---------------------------
    public async Task<ContenciosoDto> ContenciosoAbrirAsync(ComprasIntegradasContext x, ContenciosoAbrirRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "juridico_processo", "juridico_processo_movimento", "compras_contrato", "convenio_instrumento", "compras_vinculo_externo").ConfigureAwait(false);
        var origem = string.IsNullOrWhiteSpace(r.OrigemTipo) ? "" : r.OrigemTipo.Trim().ToUpperInvariant();
        if (origem is not ("CONTRATO" or "CONVENIO" or "TRABALHISTA" or "TRIBUTARIO" or "OUTRA")) throw new ContenciosoOrigemInvalidaException(origem);
        var caso = string.IsNullOrWhiteSpace(r.Caso) ? "" : r.Caso.Trim().ToUpperInvariant();
        if (caso is not ("ATIVO" or "PASSIVO")) throw new CasoContenciosoInvalidoException(caso);
        if (r.OrigemId <= 0) throw new ArgumentException("origemId é obrigatório.");
        if (r.ValorCausa < 0) throw new ArgumentException("valorCausa não pode ser negativo.");
        if (string.IsNullOrWhiteSpace(r.Assunto)) throw new ArgumentException("assunto do contencioso é obrigatório.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        if (origem == "CONTRATO")
        {
            var stCtr = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "select status from sigov.compras_contrato where id=@O and tenant_id=@T",
                new { O = r.OrigemId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (stCtr is null) throw new ContratoInexistenteException(r.OrigemId);
        }
        if (origem == "CONVENIO")
        {
            var stCvn = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "select status from sigov.convenio_instrumento where id=@O and tenant_id=@T and deleted_at is null",
                new { O = r.OrigemId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (stCvn is null) throw new InstrumentoConvenioInexistenteException(r.OrigemId);
        }
        var jaAtivo = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.juridico_processo where tenant_id=@T and ativo and not is_deleted and origem_tipo=@Ot and origem_id=@Oi and encerrado_em is null",
            new { T = x.TenantId, Ot = origem, Oi = r.OrigemId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (jaAtivo > 0) throw new ContenciosoAtivoParaOrigemException(origem, r.OrigemId);
        var numero = await c.ExecuteScalarAsync<string>(new CommandDefinition(
            "select 'JP-' || to_char(now(),'YYYYMM') || '-' || lpad(floor(random()*1000000)::int::text,6,'0')", new { }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var area = origem switch { "TRABALHISTA" => "TRABALHISTA", "TRIBUTARIO" => "TRIBUTARIO", "OUTRA" => "OUTRO", _ => "CIVEL" };
        var processoId = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.juridico_processo(tenant_id,entidade_id,descricao,status,numero_interno,area,natureza,assunto,objeto,valor_causa,instancia,vara_orgao,origem_tipo,origem_id,referencia_externa,dados,created_at,created_by)
            values(@T,@E,'Contencioso '||@Caso||' - origem '||@Ot||'/'||@Oi::text||' (BR-10.2)','ATIVO',@Num,@Area,'CONTENCIOSO',@Assunto,@Objeto,@Valor,nullif(@Inst,''),nullif(@Var,''),@Ot,@Oi,nullif(@RefEx,''),jsonb_build_object('caso',@Caso),now(),@U)
            returning id
            """, new { T = x.TenantId, E = ent, Caso = caso, Ot = origem, Oi = r.OrigemId, Num = numero, Area = area, Assunto = r.Assunto.Trim(), Objeto = "Origem " + origem + "/" + r.OrigemId.ToString() + " (caso " + caso + ")", Valor = r.ValorCausa, Inst = r.Instancia, Var = r.VaraOrgao, RefEx = r.ReferenciaExterna, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.juridico_processo_movimento(tenant_id,entidade_id,processo_id,tipo,descricao,ocorrido_em,responsavel_id)
            values(@T,@E,@P,'ABERTURA','Abertura do contencioso - origem '||@Ot||'/'||@Oi::text||', caso '||@Caso||', valor da causa '||@V::text,now(),@U)
            """, new { T = x.TenantId, E = ent, P = processoId, Ot = origem, Oi = r.OrigemId, Caso = caso, V = r.ValorCausa, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (origem == "CONTRATO")
        {
            // marcador JURIDICO do contrato (BR-10.2): ledger do vinculo - o status deriva daqui.
            await c.ExecuteAsync(new CommandDefinition(
                """
                insert into sigov.compras_vinculo_externo(tenant_id,entidade_id,contrato_id,alvo_modulo,alvo_id,status,correlation_id,created_at,created_by)
                values(@T,@E,@O,'JURIDICO_CONTENCIOSO',@P,'ATIVO',@Corr,now(),@U)
                """, new { T = x.TenantId, E = x.EntidadeId, O = r.OrigemId, P = processoId, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        var dtoAberto = await LeituraContenciosoDtoAsync(c, tx, x, processoId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 contencioso {P} aberto ({O}/{Id}, caso {C}, causa {V})", processoId, origem, r.OrigemId, caso, r.ValorCausa);
        return dtoAberto;
    }

    public async Task<ContenciosoDto> ContenciosoDesfecharAsync(ComprasIntegradasContext x, long processoId, ContenciosoDesfecharRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "juridico_processo", "juridico_processo_movimento", "compras_vinculo_externo").ConfigureAwait(false);
        var resultado = string.IsNullOrWhiteSpace(r.Resultado) ? "" : r.Resultado.Trim().ToUpperInvariant();
        if (resultado is not ("TRANSACAO" or "JUGADO" or "DESISTENCIA")) throw new ContenciosoResultadoInvalidoException(resultado);
        if (string.IsNullOrWhiteSpace(r.Justificativa)) throw new DesfechoSemJustificativaException();
        if (r.Custas < 0) throw new ArgumentException("custas não podem ser negativas.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var p = await c.QueryFirstOrDefaultAsync<(string Status, DateTime? EncerradoEm, string? Resultado, string? OrigemTipo)>(new CommandDefinition(
            "select status, encerrado_em, resultado, origem_tipo from sigov.juridico_processo where id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = processoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (p.Status is null) throw new ProcessoJuridicoInexistenteException(processoId);
        if (p.EncerradoEm is not null) throw new ContenciosoJaDesfecadoException(processoId, p.Resultado ?? "DESCONHECIDO");
        var jd = "DESFECO " + resultado + " (custas " + r.Custas.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "): " + r.Justificativa.Trim();
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.juridico_processo set status='ENCERRADO', resultado=@R, encerrado_em=now(), justificativa=@J, updated_at=now(), updated_by=@U where id=@I and tenant_id=@T",
            new { R = resultado, J = jd, U = user, I = processoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            "insert into sigov.juridico_processo_movimento(tenant_id,entidade_id,processo_id,tipo,descricao,status_anterior,status_novo,ocorrido_em,responsavel_id) values(@T,@E,@P,'DESFECO',@J,@SA,'ENCERRADO',now(),@U)",
            new { T = x.TenantId, E = x.EntidadeId, P = processoId, J = jd, SA = p.Status, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (p.OrigemTipo == "CONTRATO")
        {
            // limpa o marcador JURIDICO do contrato pelo ledger (ato registrado, sem delete fisico).
            await c.ExecuteAsync(new CommandDefinition(
                "update sigov.compras_vinculo_externo set status='QUITADO', justificativa_quitacao=@J, quitado_em=now(), quitado_por=@U, updated_at=now(), updated_by=@U where tenant_id=@T and alvo_modulo='JURIDICO_CONTENCIOSO' and alvo_id=@I and status='ATIVO' and ativo and not is_deleted",
                new { J = jd, U = user, T = x.TenantId, I = processoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        var dtoDesfecho = await LeituraContenciosoDtoAsync(c, tx, x, processoId, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 contencioso {P} desfeito ({R}, custas {C})", processoId, resultado, r.Custas);
        return dtoDesfecho;
    }

    // ---------------------------- CONTRAPARTIDA (BR-7.8) -----------------------------
    public async Task<ContrapartidaDto> ContrapartidaRegistrarAsync(ComprasIntegradasContext x, ContrapartidaRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "convenio_instrumento", "convenio_contrapartida").ConfigureAwait(false);
        var tipo = string.IsNullOrWhiteSpace(r.Tipo) ? "" : r.Tipo.Trim().ToUpperInvariant();
        if (tipo is not ("FINANCEIRA" or "BENS" or "SERVICOS" or "PESSOAL" or "OUTRA")) throw new ContrapartidaTipoInvalidoException(tipo);
        if (r.InstrumentoId <= 0) throw new ArgumentException("instrumentoId é obrigatório.");
        if (r.ValorPrevisto <= 0m || r.ValorRealizado < 0m || r.ValorRealizado > r.ValorPrevisto)
            throw new ContrapartidaValorInconsistenteException(r.ValorPrevisto, r.ValorRealizado);
        var status = r.ValorRealizado >= r.ValorPrevisto ? "REALIZADA" : (r.ValorRealizado > 0m ? "PARCIAL" : "PREVISTA");
        if (status != "PREVISTA" && r.DataRealizacao is null) throw new ContrapartidaRealizacaoSemDataException();
        if (string.IsNullOrWhiteSpace(r.Descricao)) throw new ArgumentException("descricao da contrapartida é obrigatória.");
        var chave = string.IsNullOrWhiteSpace(r.ChaveIdempotencia) ? "" : r.ChaveIdempotencia.Trim();
        if (chave.Length > 150) throw new ArgumentException("chaveIdempotencia excede 150 caracteres.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var inst = await c.QueryFirstOrDefaultAsync<(long Id, string Status, decimal Necessaria)>(new CommandDefinition(
            "select id, status, coalesce(valor_contrapartida,0) from sigov.convenio_instrumento where id=@I and tenant_id=@T and deleted_at is null",
            new { I = r.InstrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (inst.Id <= 0) throw new InstrumentoConvenioInexistenteException(r.InstrumentoId);
        if (inst.Status == "RASCUNHO") throw new InstrumentoEmRascunhoException(r.InstrumentoId);
        if (inst.Status is "ENCERRADO" or "CANCELADO") throw new InstrumentoEncerradoImutavelException(r.InstrumentoId, inst.Status);
        if (inst.Status != "ATIVO") throw new InstrumentoNaoAtivoException(r.InstrumentoId, inst.Status);
        if (!string.IsNullOrEmpty(chave))
        {
            var exist = await c.QueryFirstOrDefaultAsync<(long Id, string Tipo, decimal Previsto, decimal Realizado, string Status, string Desc)>(new CommandDefinition(
                "select id, tipo, valor_previsto, valor_realizado, status, descricao from sigov.convenio_contrapartida where tenant_id=@T and chave_idempotencia=@K and deleted_at is null limit 1",
                new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (exist.Id > 0)
            {
                if (exist.Tipo == tipo && exist.Previsto == r.ValorPrevisto && exist.Realizado == r.ValorRealizado)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return new ContrapartidaDto(exist.Id, r.InstrumentoId, exist.Tipo, exist.Desc, exist.Previsto, exist.Realizado, exist.Status, chave);
                }
                throw new ContrapartidaChaveIdempotenciaConflitoException(chave, exist.Id);
            }
        }
        var previstas = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(valor_previsto),0) from sigov.convenio_contrapartida where instrumento_id=@I and tenant_id=@T and deleted_at is null and status<>'CANCELADA'",
            new { I = r.InstrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (previstas + r.ValorPrevisto > inst.Necessaria + 0.01m)
            throw new ContrapartidaExcedeInstrumentoException(r.ValorPrevisto, Math.Max(inst.Necessaria - previstas, 0m));
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.convenio_contrapartida(tenant_id,entity_id,instrumento_id,tipo,descricao,valor_previsto,valor_realizado,data_realizacao,status,chave_idempotencia,created_at)
            values(@T,@E,@I,@Tipo,@Desc,@Pv,@Rz,@Dr,@St,@K,now())
            returning id
            """, new { T = x.TenantId, E = ent, I = r.InstrumentoId, Tipo = tipo, Desc = r.Descricao.Trim(), Pv = r.ValorPrevisto, Rz = r.ValorRealizado, Dr = r.DataRealizacao, St = status, K = string.IsNullOrEmpty(chave) ? null : chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 contrapartida {Id} registrada no instrumento {I} ({Tipo} previsto {Pv} realizado {Rz} {St})", id, r.InstrumentoId, tipo, r.ValorPrevisto, r.ValorRealizado, status);
        return new ContrapartidaDto(id, r.InstrumentoId, tipo, r.Descricao.Trim(), r.ValorPrevisto, r.ValorRealizado, status, string.IsNullOrEmpty(chave) ? null : chave);
    }

    public async Task<VinculoExternoDto> VinculoConvenioAplicarAsync(ComprasIntegradasContext x, VinculoConvenioAplicarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "convenio_instrumento", "convenio_contrapartida", "compras_vinculo_externo").ConfigureAwait(false);
        if (r.ContratoId <= 0 || r.InstrumentoId <= 0) throw new ArgumentException("contratoId e instrumentoId são obrigatórios.");
        var chave = string.IsNullOrWhiteSpace(r.ChaveIdempotencia) ? "" : r.ChaveIdempotencia.Trim();
        if (chave.Length > 150) throw new ArgumentException("chaveIdempotencia excede 150 caracteres.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var stCtr = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select status from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = r.ContratoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (stCtr is null) throw new ContratoInexistenteException(r.ContratoId);
        if (stCtr is not ("VIGENTE" or "PRORROGADO")) throw new ContratoNaoVigenteParaVinculoException(r.ContratoId, stCtr);
        var inst = await c.QueryFirstOrDefaultAsync<(long Id, string Status, decimal Necessaria)>(new CommandDefinition(
            "select id, status, coalesce(valor_contrapartida,0) from sigov.convenio_instrumento where id=@I and tenant_id=@T and deleted_at is null",
            new { I = r.InstrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (inst.Id <= 0) throw new InstrumentoConvenioInexistenteException(r.InstrumentoId);
        if (inst.Status != "ATIVO") throw new InstrumentoNaoAtivoException(r.InstrumentoId, inst.Status);
        if (inst.Necessaria > 0m)
        {
            // gate BR-7.8: recurso proprio exigido pelo instrumento tem que estar REALIZADO antes de aplicar.
            var realizado = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
                "select coalesce(sum(valor_realizado),0) from sigov.convenio_contrapartida where instrumento_id=@I and tenant_id=@T and deleted_at is null and status<>'CANCELADA'",
                new { I = r.InstrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (realizado + 0.01m < inst.Necessaria)
                throw new ContrapartidaConvenioInexistenteException(r.InstrumentoId, inst.Necessaria, realizado);
        }
        if (!string.IsNullOrEmpty(chave))
        {
            var exist = await c.QueryFirstOrDefaultAsync<(long Id, long Contrato, long Alvo, string Status, string? Just)>(new CommandDefinition(
                "select id, contrato_id, alvo_id, status, justificativa_quitacao from sigov.compras_vinculo_externo where tenant_id=@T and chave_idempotencia=@K and ativo and not is_deleted limit 1",
                new { T = x.TenantId, K = chave }, tx, cancellationToken: ct)).ConfigureAwait(false);
            if (exist.Id > 0)
            {
                if (exist.Contrato == r.ContratoId && exist.Alvo == r.InstrumentoId)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    return new VinculoExternoDto(exist.Id, exist.Contrato, "CONVENIO", exist.Alvo, exist.Status, chave, exist.Just);
                }
                throw new VinculoConvenioChaveConflitoException(chave, exist.Id);
            }
        }
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.compras_vinculo_externo(tenant_id,entidade_id,contrato_id,alvo_modulo,alvo_id,status,chave_idempotencia,correlation_id,created_at,created_by)
            values(@T,@E,@C,'CONVENIO',@I,'ATIVO',@K,@Corr,now(),@U)
            returning id
            """, new { T = x.TenantId, E = x.EntidadeId, C = r.ContratoId, I = r.InstrumentoId, K = string.IsNullOrEmpty(chave) ? null : chave, Corr = GuidCorr(x.CorrelationId), U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 vinculo do convenio {I} aplicado ao contrato {C}", r.InstrumentoId, r.ContratoId);
        return new VinculoExternoDto(id, r.ContratoId, "CONVENIO", r.InstrumentoId, "ATIVO", string.IsNullOrEmpty(chave) ? null : chave, null);
    }

    public async Task<VinculoExternoDto> VinculoQuitarAsync(ComprasIntegradasContext x, long vinculoId, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_vinculo_externo").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(justificativa)) throw new QuitarVinculoSemJustificativaException();
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var v = await c.QueryFirstOrDefaultAsync<(long Id, long Contrato, string AlvoModulo, long Alvo, string Status)>(new CommandDefinition(
            "select id, contrato_id, alvo_modulo, alvo_id, status from sigov.compras_vinculo_externo where id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = vinculoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (v.Id <= 0) throw new VinculoExternoInexistenteException(vinculoId);
        if (v.Status == "QUITADO") throw new VinculoJaQuitadoException(vinculoId);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.compras_vinculo_externo set status='QUITADO', justificativa_quitacao=@J, quitado_em=now(), quitado_por=@U, updated_at=now(), updated_by=@U where id=@I and tenant_id=@T",
            new { J = justificativa.Trim(), U = user, I = vinculoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 vinculo {I} ({M}/{A}) quitado com justificativa", vinculoId, v.AlvoModulo, v.Alvo);
        return new VinculoExternoDto(v.Id, v.Contrato, v.AlvoModulo, v.Alvo, "QUITADO", null, justificativa.Trim());
    }

    // ------------------------- CONVENIOS (BR-10.3/BR-10.5) ---------------------------
    public async Task<ConvencioEncerradoDto> ConvencioEncerrarAsync(ComprasIntegradasContext x, long instrumentoId, string? justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "convenio_instrumento", "compras_vinculo_externo", "convenio_repasse", "convenio_etapa", "convenio_meta", "convenio_projeto").ConfigureAwait(false);
        if (instrumentoId <= 0) throw new ArgumentException("instrumentoId é obrigatório.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var inst = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.convenio_instrumento where id=@I and tenant_id=@T and deleted_at is null",
            new { I = instrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (inst.Id <= 0) throw new InstrumentoConvenioInexistenteException(instrumentoId);
        if (inst.Status is "ENCERRADO" or "CANCELADO") throw new InstrumentoJaEncerradoException(instrumentoId, inst.Status);
        if (inst.Status == "RASCUNHO") throw new InstrumentoEmRascunhoException(instrumentoId);
        // cascata BR-10.5: encerramento bloqueado enquanto contrato vinculado estiver ATIVO no ledger.
        var vinculados = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.compras_vinculo_externo where tenant_id=@T and alvo_modulo='CONVENIO' and alvo_id=@I and status='ATIVO' and ativo and not is_deleted",
            new { T = x.TenantId, I = instrumentoId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (vinculados > 0) throw new ConvenioEncerramentoBloqueadoContratoAtivoException(instrumentoId, (int)vinculados);
        var (recebido, executado) = await ExecucaoConvenioAsync(c, tx, x, instrumentoId, ct).ConfigureAwait(false);
        var saldo = recebido - executado;
        if (saldo > 0.01m && string.IsNullOrWhiteSpace(justificativa)) throw new EncerrarConvenioSaldoSemJustificativaException(saldo);
        var obs = "ENCERRADO (frente integrada C2S10)" + (saldo > 0.01m ? " COM SALDO " + saldo.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ": " + justificativa!.Trim() : "");
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.convenio_instrumento set status='ENCERRADO', observacao=coalesce(observacao,'')||' | '||@O, updated_at=now() where id=@I and tenant_id=@T",
            new { O = obs, U = user, I = instrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 instrumento de convenio {I} encerrado (saldo de execucao {S})", instrumentoId, saldo);
        return new ConvencioEncerradoDto(instrumentoId, "ENCERRADO", saldo);
    }

    public async Task<PrestacaoDto> PrestacaoRegistrarAsync(ComprasIntegradasContext x, PrestacaoRegistrarRequest r, CancellationToken ct)
    {
        await GuardAsync(ct, "convenio_instrumento", "convenio_prestacao_contas", "convenio_repasse", "convenio_etapa", "convenio_meta", "convenio_projeto").ConfigureAwait(false);
        var tipo = string.IsNullOrWhiteSpace(r.Tipo) ? "" : r.Tipo.Trim().ToUpperInvariant();
        if (tipo is not ("PARCIAL" or "FINAL")) throw new PrestacaoTipoInvalidoException(tipo);
        if (string.IsNullOrWhiteSpace(r.NumeroPrestacao)) throw new ArgumentException("numeroPrestacao é obrigatório.");
        if (r.PeriodoFinal < r.PeriodoInicial) throw new ArgumentException("periodoFinal deve ser posterior ou igual ao periodoInicial.");
        if (r.ValorPrestado < 0m) throw new ArgumentException("valorPrestado não pode ser negativo.");
        var ent = x.EntidadeId ?? throw new InvalidOperationException("entidade_id obrigatório no contexto institucional.");
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var inst = await c.QueryFirstOrDefaultAsync<(long Id, string Status)>(new CommandDefinition(
            "select id, status from sigov.convenio_instrumento where id=@I and tenant_id=@T and deleted_at is null",
            new { I = r.InstrumentoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (inst.Id <= 0) throw new InstrumentoConvenioInexistenteException(r.InstrumentoId);
        if (inst.Status != "ATIVO") throw new InstrumentoNaoAtivoException(r.InstrumentoId, inst.Status);
        var dup = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(1) from sigov.convenio_prestacao_contas where instrumento_id=@I and tenant_id=@T and deleted_at is null and numero_prestacao=@N",
            new { I = r.InstrumentoId, T = x.TenantId, N = r.NumeroPrestacao.Trim() }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (dup > 0) throw new PrestacaoNumeroDuplicadoException(r.InstrumentoId, r.NumeroPrestacao.Trim());
        var minimo = (int)await LireParametroIntConveniosAsync(c, tx, x, "CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT", ct).ConfigureAwait(false);
        var (recebido, executado) = await ExecucaoConvenioAsync(c, tx, x, r.InstrumentoId, ct).ConfigureAwait(false);
        var pct = recebido > 0m ? executado / recebido * 100m : 0m;
        if (pct + 0.005m < minimo) throw new PrestacaoExecucaoMinimaNaoAtingidaException((double)pct, minimo);
        var saldo = recebido - executado;
        if (tipo == "FINAL" && saldo > 0.01m) throw new PrestacaoFinalSaldoNaoJustificadoException(r.InstrumentoId, saldo);
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            insert into sigov.convenio_prestacao_contas(tenant_id,entity_id,instrumento_id,numero_prestacao,tipo,periodo_inicio,periodo_fim,data_envio,valor_prestado,status,parecer_resumo)
            values(@T,@E,@I,@N,@Tipo,@PI,@PF,@De,@V,'ENVIADA',nullif(@Pr,''))
            returning id
            """, new { T = x.TenantId, E = ent, I = r.InstrumentoId, N = r.NumeroPrestacao.Trim(), Tipo = tipo, PI = r.PeriodoInicial, PF = r.PeriodoFinal, De = clock.DataAtual, V = r.ValorPrestado, Pr = r.ParecerResumo }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 prestacao de contas {N} ({Tipo}) registrada no instrumento {I} (execucao {P:Pct}%, minimo parametrico {M:Pct}%)", r.NumeroPrestacao, tipo, r.InstrumentoId, pct, minimo);
        return new PrestacaoDto(id, r.InstrumentoId, tipo, r.NumeroPrestacao.Trim(), r.ValorPrestado, "ENVIADA");
    }

    // ------------------------------- OBRAS (BR-10.4) ---------------------------------
    public async Task<MedicaoHomologadaDto> MedicaoHomologarAsync(ComprasIntegradasContext x, long medicaoId, CancellationToken ct)
    {
        await GuardAsync(ct, "obras_medicao", "obras_medicao_aprovacao", "obras_obra").ConfigureAwait(false);
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        var med = await c.QueryFirstOrDefaultAsync<(long Id, long Obra, string Status, decimal? Valor, decimal PctFisico, decimal SaldoAntes)>(new CommandDefinition(
            "select id, obra_id, status, valor, coalesce(percentual_fisico,0), coalesce(saldo_contratual_antes,0) from sigov.obras_medicao where id=@I and tenant_id=@T and ativo and not is_deleted",
            new { I = medicaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (med.Id <= 0) throw new MedicaoObrasInexistenteException(medicaoId);
        if (med.Status == "HOMOLOGADA") throw new MedicaoJaHomologadaException(medicaoId);
        if (med.Status != "ENVIADA") throw new MedicaoStatusInvalidoException(medicaoId, med.Status);
        if (med.Valor is null || med.Valor.Value < 0m) throw new MedicaoStatusInvalidoException(medicaoId, "SEM_VALOR");
        var obraVl = await c.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "select coalesce(valor_contratado,0) from sigov.obras_obra where id=@O and tenant_id=@T and ativo and not is_deleted",
            new { O = med.Obra, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (obraVl is null || obraVl.Value <= 0m) throw new ObraInexistenteException(med.Obra);
        var homologados = await c.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "select coalesce(sum(valor),0) from sigov.obras_medicao where obra_id=@O and tenant_id=@T and ativo and not is_deleted and status='HOMOLOGADA'",
            new { O = med.Obra, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var totalFin = homologados + med.Valor.Value;
        var pctFin = obraVl.Value > 0m ? totalFin / obraVl.Value * 100m : 0m;
        // gate BR-10.4: pagamento financeiro acumulado acima do medido fisicamente (ou acima do
        // contratado) bloqueia a homologacao - OBRA_MEDICAO_INSUFICIENTE.
        if (totalFin > obraVl.Value + 0.01m) throw new ObraMedicaoInsuficienteException((double)Math.Round(pctFin, 2), 100d);
        if (pctFin > med.PctFisico + 0.005m) throw new ObraMedicaoInsuficienteException((double)Math.Round(pctFin, 2), (double)med.PctFisico);
        if (med.Valor.Value > med.SaldoAntes + 0.01m) throw new ObraMedicaoInsuficienteException((double)Math.Round(pctFin, 2), (double)med.PctFisico);
        var depois = Math.Max(med.SaldoAntes - med.Valor.Value, 0m);
        await c.ExecuteAsync(new CommandDefinition(
            "update sigov.obras_medicao set status='HOMOLOGADA', homologada_at=now(), saldo_contratual_depois=@SD, updated_at=now(), updated_by=@U where id=@I and tenant_id=@T",
            new { SD = depois, U = user, I = medicaoId, T = x.TenantId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await c.ExecuteAsync(new CommandDefinition(
            """
            insert into sigov.obras_medicao_aprovacao(tenant_id,entidade_id,medicao_id,aprovador_id,alcada,decisao,justificativa,created_at,created_by)
            values(@T,@E,@M,@U,'FISCAL_OBRA','APROVADA','Homologacao integrada da frente administrativa C2S10 (BR-10.4)',now(),@U)
            """, new { T = x.TenantId, E = x.EntidadeId, M = medicaoId, U = user }, tx, cancellationToken: ct)).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S10 medicao {I} homologada (obra {O}, valor {V}, financeiro acumulado {P:Pct}%)", medicaoId, med.Obra, med.Valor.Value, pctFin);
        return new MedicaoHomologadaDto(medicaoId, med.Obra, med.Valor.Value, med.PctFisico, "HOMOLOGADA", depois);
    }

    // ---------------------------------- GET VINCULOS ---------------------------------
    public async Task<IReadOnlyList<VinculoExternoDto>> VinculosContratoAsync(ComprasIntegradasContext x, long contratoId, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_vinculo_externo", "compras_contrato").ConfigureAwait(false);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        var st = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "select status from sigov.compras_contrato where id=@C and tenant_id=@T",
            new { C = contratoId, T = x.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (st is null) throw new ContratoInexistenteException(contratoId);
        var linhas = (await c.QueryAsync<(long Id, long Contrato, string AlvoModulo, long Alvo, string Status, string? Chave, string? Just)>(new CommandDefinition(
            """
            select id, contrato_id, alvo_modulo, alvo_id, status, chave_idempotencia, justificativa_quitacao
            from sigov.compras_vinculo_externo
            where tenant_id=@T and contrato_id=@C and ativo and not is_deleted
            order by id
            """, new { T = x.TenantId, C = contratoId }, cancellationToken: ct))).Select(v => new VinculoExternoDto(v.Id, v.Contrato, v.AlvoModulo, v.Alvo, v.Status, v.Chave, v.Just)).ToList();
        return linhas;
    }
}
