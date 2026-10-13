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
        return (await c.QueryAsync<DemandaIntegradaDto>(new CommandDefinition(
            """
            select d.id, d.objeto, d.status, d.urgencia, d.valor_estimado,
                   (select count(1) from sigov.compras_demanda_item i where i.demanda_id=d.id and i.ativo=true and not i.is_deleted) as Nitens,
                   d.centro_custo_id as CentroCustoId,
                   (select p.id from sigov.compras_processo p where p.tenant_id=d.tenant_id and p.demanda_id=d.id and p.status not in ('CANCELADO','FRACASSADO','DESERTO') limit 1) as ProcessoId,
                   d.created_by as CriadoPor, d.aprovado_por as AprovadoPor, d.dupla_aprovado_por as DuplaAprovadoPor, d.created_at as CriadoEm
            from sigov.compras_demanda d where d.tenant_id=@T and (@E::bigint is null or d.entidade_id=@E)
            order by d.created_at desc limit 200
            """, new { T = x.TenantId, E = x.EntidadeId }, cancellationToken: ct))).AsList();
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
        await GuardAsync(ct, "compras_licitacao", "compras_adjudicacao", "compras_homologacao").ConfigureAwait(false);
        var user = UsuarioObrigatorio(x);
        await using var c = factory.CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await CarregarLicitacaoAsync(c, tx, x, licitacaoId, ct).ConfigureAwait(false);

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
        await GuardAsync(ct, "compras_homologacao", "compras_adjudicacao", "compras_adjudicacao_item", "compras_proposta", "compras_licitacao", "compras_contrato", "compras_contrato_item").ConfigureAwait(false);
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
        await tx.CommitAsync(ct).ConfigureAwait(false);
        log.LogInformation("C2S8 contrato {Id} gerado da homologacao {H} (valor derivado {V})", contratoId, r.HomologacaoId, adj.ValorTotal);
        return new ContratoGeradoDto(contratoId, numero, vencedorFornecedor, Math.Round(adj.ValorTotal, 2), itensAdj.Count, r.DataEficacia, r.DataFim, modo, "VIGENTE");
    }

    public async Task<ContratoExecucaoDto> ContratoExecucaoAsync(ComprasIntegradasContext x, long contratoId, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "compras_recebimento", "compras_recebimento_item").ConfigureAwait(false);
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
        return new ContratoExecucaoDto(contratoId, contrato.Valor, fornecido, pct, (int)teto, pct >= teto);
    }

    public async Task ContratoProrrogarAsync(ComprasIntegradasContext x, long contratoId, DateOnly novoFim, string justificativa, CancellationToken ct)
    {
        await GuardAsync(ct, "compras_contrato", "compras_contrato_historico").ConfigureAwait(false);
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
}
