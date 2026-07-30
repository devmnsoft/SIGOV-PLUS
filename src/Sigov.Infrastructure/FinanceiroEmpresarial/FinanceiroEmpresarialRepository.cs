using System.Data;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.FinanceiroEmpresarial;
using Sigov.Domain.FinanceiroEmpresarial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.FinanceiroEmpresarial;

public sealed class FinanceiroEmpresarialRepository(DapperContext context) : IFinanceiroEmpresarialRepository
{
    public Task<PagedResult<ContaFinanceiraResumoDto>> ListarReceberAsync(long tenantId, ContaFinanceiraFiltro filtro, CancellationToken ct) => ListarAsync("financeiro_conta_receber", tenantId, filtro, ct);
    public Task<PagedResult<ContaFinanceiraResumoDto>> ListarPagarAsync(long tenantId, ContaFinanceiraFiltro filtro, CancellationToken ct) => ListarAsync("financeiro_conta_pagar", tenantId, filtro, ct);

    private async Task<PagedResult<ContaFinanceiraResumoDto>> ListarAsync(string tabela, long tenantId, ContaFinanceiraFiltro filtro, CancellationToken ct)
    {
        var page = Math.Max(1, filtro.Page); var size = Math.Clamp(filtro.PageSize, 1, 100);
        using var c = context.CreateConnection();
        var sql = $@"select count(*) from sigov.{tabela} where tenant_id=@TenantId and not is_deleted and (@Status is null or status=@Status) and (@Inicio is null or vencimento>=@Inicio) and (@Fim is null or vencimento<=@Fim);
select id,descricao,valor_original ValorOriginal,valor_aberto ValorAberto,vencimento,status,version,origem from sigov.{tabela} where tenant_id=@TenantId and not is_deleted and (@Status is null or status=@Status) and (@Inicio is null or vencimento>=@Inicio) and (@Fim is null or vencimento<=@Fim) order by vencimento,id offset @Offset limit @Limit;";
        var cmd = new CommandDefinition(sql, new { TenantId = tenantId, filtro.Status, Inicio = filtro.VencimentoInicio, Fim = filtro.VencimentoFim, Offset = (page - 1) * size, Limit = size }, cancellationToken: ct);
        using var multi = await c.QueryMultipleAsync(cmd).ConfigureAwait(false);
        var total = await multi.ReadSingleAsync<int>().ConfigureAwait(false);
        var items = (await multi.ReadAsync<ContaFinanceiraResumoDto>().ConfigureAwait(false)).AsList();
        return new PagedResult<ContaFinanceiraResumoDto>(items, page, size, total);
    }

    public Task<long> CriarReceberAsync(long tenantId, CriarContaReceberRequest r, long? user, Guid cid, CancellationToken ct) => CriarAsync(true, tenantId, r.Descricao, r.Valor, r.Vencimento, r.ClienteId, r.NaturezaId, r.CentroCustoId, r.DocumentoReferencia, r.IdempotencyKey, user, cid, ct);
    public Task<long> CriarPagarAsync(long tenantId, CriarContaPagarRequest r, long? user, Guid cid, CancellationToken ct) => CriarAsync(false, tenantId, r.Descricao, r.Valor, r.Vencimento, r.FornecedorId, r.NaturezaId, r.CentroCustoId, r.DocumentoReferencia, r.IdempotencyKey, user, cid, ct);

    private async Task<long> CriarAsync(bool receber, long tenantId, string descricao, decimal valor, DateOnly vencimento, long? pessoaId, long? naturezaId, long? centroId, string? documento, string key, long? user, Guid cid, CancellationToken ct)
    {
        RegrasFinanceiras.ValorEfetivo(valor, 0, 0);
        if (string.IsNullOrWhiteSpace(descricao) || string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Descrição e idempotency key são obrigatórias.");
        var tabela = receber ? "financeiro_conta_receber" : "financeiro_conta_pagar";
        var pessoa = receber ? "cliente_id" : "fornecedor_id";
        var evento = receber ? "financeiro.conta_receber.criada" : "financeiro.conta_pagar.criada";
        using var c = context.CreateConnection(); c.Open(); using var tx = c.BeginTransaction();
        var sql = $@"insert into sigov.{tabela}(tenant_id,origem,descricao,valor_original,valor_aberto,vencimento,competencia,status,natureza_id,centro_custo_id,{pessoa},documento_referencia,idempotency_key,correlation_id,created_by)
values(@TenantId,'MANUAL',@Descricao,@Valor,@Valor,@Vencimento,@Vencimento,'ABERTA',@NaturezaId,@CentroId,@PessoaId,@Documento,@Key,@Cid,@User)
on conflict(tenant_id,idempotency_key) do update set idempotency_key=excluded.idempotency_key returning id;";
        var id = await c.QuerySingleAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, Descricao = descricao.Trim(), Valor = valor, Vencimento = vencimento, NaturezaId = naturezaId, CentroId = centroId, PessoaId = pessoaId, Documento = documento, Key = key, Cid = cid, User = user }, tx, cancellationToken: ct));
        await RegistrarAsync(c, tx, tenantId, evento, receber ? "conta_receber" : "conta_pagar", id, user, cid, key, new { id, valor, vencimento }, ct);
        tx.Commit(); return id;
    }

    public Task<BaixaFinanceiraDto> BaixarReceberAsync(long tenantId, long id, BaixarContaRequest r, long? user, Guid cid, CancellationToken ct) => BaixarAsync(true, tenantId, id, r, user, cid, ct);
    public Task<BaixaFinanceiraDto> BaixarPagarAsync(long tenantId, long id, BaixarContaRequest r, long? user, Guid cid, CancellationToken ct) => BaixarAsync(false, tenantId, id, r, user, cid, ct);

    private async Task<BaixaFinanceiraDto> BaixarAsync(bool receber, long tenantId, long id, BaixarContaRequest r, long? user, Guid cid, CancellationToken ct)
    {
        var efetivo = RegrasFinanceiras.ValorEfetivo(r.Valor, r.Desconto, r.Acrescimo);
        if ((r.ContaBancariaId is null) == (r.CaixaId is null) || string.IsNullOrWhiteSpace(r.IdempotencyKey)) throw new InvalidOperationException("Informe exatamente uma conta bancária ou caixa e a chave de idempotência.");
        var tabela = receber ? "financeiro_conta_receber" : "financeiro_conta_pagar"; var baixa = receber ? "financeiro_baixa_receber" : "financeiro_baixa_pagar";
        using var c = context.CreateConnection(); c.Open(); using var tx = c.BeginTransaction(IsolationLevel.ReadCommitted);
        var titulo = await c.QuerySingleOrDefaultAsync<TituloLock>(new CommandDefinition($"select id,valor_aberto ValorAberto,status,version from sigov.{tabela} where tenant_id=@TenantId and id=@Id and not is_deleted for update", new { TenantId = tenantId, Id = id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Título não encontrado.");
        if (titulo.Version != r.Version) throw new DBConcurrencyException("Versão desatualizada.");
        if (titulo.Status is "CANCELADA" or "RECEBIDA" or "PAGA" or "RENEGOCIADA") throw new InvalidOperationException("Transição inválida para baixa.");
        var parcial = await c.QuerySingleAsync<bool>(new CommandDefinition("select permitir_baixa_parcial from sigov.financeiro_configuracao where tenant_id=@TenantId", new { TenantId = tenantId }, tx, cancellationToken: ct));
        RegrasFinanceiras.ValidarBaixa(titulo.ValorAberto, r.Valor, parcial);
        var saldo = await AlterarSaldoAsync(c, tx, tenantId, r.ContaBancariaId, r.CaixaId, receber ? efetivo : -efetivo, ct);
        var tipo = receber ? "ENTRADA" : "SAIDA"; var status = r.Valor == titulo.ValorAberto ? (receber ? "RECEBIDA" : "PAGA") : "PARCIAL";
        var movimentoId = await c.QuerySingleAsync<long>(new CommandDefinition("insert into sigov.financeiro_movimento(tenant_id,conta_bancaria_id,caixa_id,tipo,origem,origem_id,descricao,valor,usuario_id,correlation_id,idempotency_key) values(@TenantId,@Conta,@Caixa,@Tipo,@Origem,@Id,@Descricao,@Valor,@User,@Cid,@Key) returning id", new { TenantId = tenantId, Conta = r.ContaBancariaId, Caixa = r.CaixaId, Tipo = tipo, Origem = receber ? "CONTA_RECEBER" : "CONTA_PAGAR", Id = id, Descricao = receber ? "Recebimento de título" : "Pagamento de título", Valor = efetivo, User = user, Cid = cid, Key = r.IdempotencyKey }, tx, cancellationToken: ct));
        var baixaId = await c.QuerySingleAsync<long>(new CommandDefinition($"insert into sigov.{baixa}(tenant_id,conta_{(receber ? "receber" : "pagar")}_id,movimento_id,valor,desconto,acrescimo,forma_pagamento_id,conta_bancaria_id,caixa_id,usuario_id,correlation_id,idempotency_key) values(@TenantId,@Id,@Movimento,@Valor,@Desconto,@Acrescimo,@Forma,@Conta,@Caixa,@User,@Cid,@Key) returning id", new { TenantId = tenantId, Id = id, Movimento = movimentoId, Valor = r.Valor, r.Desconto, r.Acrescimo, Forma = r.FormaPagamentoId, Conta = r.ContaBancariaId, Caixa = r.CaixaId, User = user, Cid = cid, Key = r.IdempotencyKey }, tx, cancellationToken: ct));
        var version = await c.QuerySingleAsync<long>(new CommandDefinition($"update sigov.{tabela} set valor_aberto=valor_aberto-@Valor,status=@Status,version=version+1,updated_by=@User where tenant_id=@TenantId and id=@Id and version=@Version returning version", new { TenantId = tenantId, Id = id, Valor = r.Valor, Status = status, User = user, Version = r.Version }, tx, cancellationToken: ct));
        await RegistrarAsync(c, tx, tenantId, receber ? "financeiro.conta_receber.baixada" : "financeiro.conta_pagar.paga", receber ? "conta_receber" : "conta_pagar", id, user, cid, r.IdempotencyKey + ":event", new { baixaId, movimentoId, r.Valor, efetivo, status }, ct);
        tx.Commit(); return new(id, baixaId, movimentoId, efetivo, titulo.ValorAberto - r.Valor, saldo, status, version);
    }

    public Task<BaixaFinanceiraDto> EstornarReceberAsync(long tenantId, long id, EstornarBaixaRequest r, long? user, Guid cid, CancellationToken ct) => EstornarAsync(true, tenantId, id, r, user, cid, ct);
    public Task<BaixaFinanceiraDto> EstornarPagarAsync(long tenantId, long id, EstornarBaixaRequest r, long? user, Guid cid, CancellationToken ct) => EstornarAsync(false, tenantId, id, r, user, cid, ct);

    private async Task<BaixaFinanceiraDto> EstornarAsync(bool receber, long tenantId, long id, EstornarBaixaRequest r, long? user, Guid cid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Motivo) || string.IsNullOrWhiteSpace(r.IdempotencyKey)) throw new InvalidOperationException("Motivo e idempotency key são obrigatórios.");
        var tabela = receber ? "financeiro_conta_receber" : "financeiro_conta_pagar"; var baixaTabela = receber ? "financeiro_baixa_receber" : "financeiro_baixa_pagar";
        using var c = context.CreateConnection(); c.Open(); using var tx = c.BeginTransaction();
        var titulo = await c.QuerySingleOrDefaultAsync<TituloLock>(new CommandDefinition($"select id,valor_aberto ValorAberto,status,version from sigov.{tabela} where tenant_id=@TenantId and id=@Id and not is_deleted for update", new { TenantId = tenantId, Id = id }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Título não encontrado.");
        if (titulo.Version != r.Version) throw new DBConcurrencyException("Versão desatualizada.");
        var b = await c.QuerySingleOrDefaultAsync<BaixaLock>(new CommandDefinition($"select b.id,b.valor,b.desconto,b.acrescimo,b.movimento_id MovimentoId,b.conta_bancaria_id ContaId,b.caixa_id CaixaId,b.estornado from sigov.{baixaTabela} b where b.tenant_id=@TenantId and b.conta_{(receber ? "receber" : "pagar")}_id=@Id and b.id=@BaixaId for update", new { TenantId = tenantId, Id = id, r.BaixaId }, tx, cancellationToken: ct)) ?? throw new KeyNotFoundException("Baixa não encontrada.");
        if (b.Estornado) throw new InvalidOperationException("Baixa já estornada.");
        var efetivo = b.Valor - b.Desconto + b.Acrescimo; var saldo = await AlterarSaldoAsync(c, tx, tenantId, b.ContaId, b.CaixaId, receber ? -efetivo : efetivo, ct);
        var inverso = receber ? "ESTORNO_SAIDA" : "ESTORNO_ENTRADA";
        var movimentoId = await c.QuerySingleAsync<long>(new CommandDefinition("insert into sigov.financeiro_movimento(tenant_id,conta_bancaria_id,caixa_id,tipo,origem,origem_id,descricao,valor,usuario_id,correlation_id,idempotency_key,movimento_original_id) values(@TenantId,@Conta,@Caixa,@Tipo,@Origem,@Id,@Descricao,@Valor,@User,@Cid,@Key,@Original) returning id", new { TenantId = tenantId, Conta = b.ContaId, Caixa = b.CaixaId, Tipo = inverso, Origem = receber ? "CONTA_RECEBER" : "CONTA_PAGAR", Id = id, Descricao = r.Motivo, Valor = efetivo, User = user, Cid = cid, Key = r.IdempotencyKey, Original = b.MovimentoId }, tx, cancellationToken: ct));
        await c.ExecuteAsync(new CommandDefinition($"update sigov.{baixaTabela} set estornado=true,estornado_em=now(),estornado_por=@User,estorno_motivo=@Motivo,movimento_estorno_id=@Movimento where tenant_id=@TenantId and id=@BaixaId; update sigov.financeiro_movimento set estornado=true,movimento_estorno_id=@Movimento where tenant_id=@TenantId and id=@Original", new { TenantId = tenantId, r.BaixaId, User = user, r.Motivo, Movimento = movimentoId, Original = b.MovimentoId }, tx, cancellationToken: ct));
        var novoAberto = titulo.ValorAberto + b.Valor; var status = novoAberto == b.Valor ? "ABERTA" : "PARCIAL";
        var version = await c.QuerySingleAsync<long>(new CommandDefinition($"update sigov.{tabela} set valor_aberto=@Aberto,status=@Status,version=version+1,updated_by=@User where tenant_id=@TenantId and id=@Id and version=@Version returning version", new { TenantId = tenantId, Id = id, Aberto = novoAberto, Status = status, User = user, Version = r.Version }, tx, cancellationToken: ct));
        await RegistrarAsync(c, tx, tenantId, receber ? "financeiro.conta_receber.estornada" : "financeiro.conta_pagar.estornada", receber ? "conta_receber" : "conta_pagar", id, user, cid, r.IdempotencyKey + ":event", new { baixaId = b.Id, movimentoOriginalId = b.MovimentoId, movimentoEstornoId = movimentoId, r.Motivo }, ct);
        tx.Commit(); return new(id, b.Id, movimentoId, efetivo, novoAberto, saldo, status, version);
    }

    public async Task<TransferenciaFinanceiraDto> TransferirAsync(long tenantId, TransferirValoresRequest r, long? user, Guid cid, CancellationToken ct)
    {
        RegrasFinanceiras.ValorEfetivo(r.Valor, 0, 0); if (r.ContaOrigemId == r.ContaDestinoId || string.IsNullOrWhiteSpace(r.IdempotencyKey)) throw new InvalidOperationException("Contas distintas e idempotency key são obrigatórias.");
        using var c = context.CreateConnection(); c.Open(); using var tx = c.BeginTransaction();
        var contas = (await c.QueryAsync<ContaLock>(new CommandDefinition("select id,saldo_atual Saldo from sigov.financeiro_conta_bancaria where tenant_id=@TenantId and id=any(@Ids) and ativo and not is_deleted order by id for update", new { TenantId = tenantId, Ids = new[] { r.ContaOrigemId, r.ContaDestinoId } }, tx, cancellationToken: ct))).AsList();
        if (contas.Count != 2) throw new KeyNotFoundException("Conta bancária inexistente ou inativa."); var origem = contas.Single(x => x.Id == r.ContaOrigemId); if (origem.Saldo < r.Valor) throw new InvalidOperationException("Saldo insuficiente.");
        var transferenciaId = await c.QuerySingleAsync<long>(new CommandDefinition("insert into sigov.financeiro_transferencia(tenant_id,conta_origem_id,conta_destino_id,valor,descricao,status,idempotency_key,correlation_id,created_by) values(@TenantId,@Origem,@Destino,@Valor,@Descricao,'CONCLUIDA',@Key,@Cid,@User) on conflict(tenant_id,idempotency_key) do update set idempotency_key=excluded.idempotency_key returning id", new { TenantId = tenantId, Origem = r.ContaOrigemId, Destino = r.ContaDestinoId, r.Valor, r.Descricao, Key = r.IdempotencyKey, Cid = cid, User = user }, tx, cancellationToken: ct));
        var saida = await InserirMovimentoTransferencia(c, tx, tenantId, r.ContaOrigemId, "TRANSFERENCIA_SAIDA", transferenciaId, r, user, cid, r.IdempotencyKey + ":saida", ct); var entrada = await InserirMovimentoTransferencia(c, tx, tenantId, r.ContaDestinoId, "TRANSFERENCIA_ENTRADA", transferenciaId, r, user, cid, r.IdempotencyKey + ":entrada", ct);
        await c.ExecuteAsync(new CommandDefinition("update sigov.financeiro_conta_bancaria set saldo_atual=saldo_atual-@Valor,version=version+1 where tenant_id=@TenantId and id=@Origem; update sigov.financeiro_conta_bancaria set saldo_atual=saldo_atual+@Valor,version=version+1 where tenant_id=@TenantId and id=@Destino; update sigov.financeiro_transferencia set movimento_saida_id=@Saida,movimento_entrada_id=@Entrada where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Origem = r.ContaOrigemId, Destino = r.ContaDestinoId, r.Valor, Saida = saida, Entrada = entrada, Id = transferenciaId }, tx, cancellationToken: ct));
        await RegistrarAsync(c, tx, tenantId, "financeiro.transferencia.concluida", "transferencia", transferenciaId, user, cid, r.IdempotencyKey + ":event", new { saida, entrada, r.Valor }, ct); tx.Commit();
        return new(transferenciaId, saida, entrada, origem.Saldo - r.Valor, contas.Single(x => x.Id == r.ContaDestinoId).Saldo + r.Valor);
    }

    public async Task<FinanceiroDashboardDto> DashboardAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var c = context.CreateConnection(); var cmd = new CommandDefinition(@"select coalesce((select sum(saldo_atual) from sigov.financeiro_conta_bancaria where tenant_id=@TenantId and ativo and not is_deleted),0) SaldoConsolidado,coalesce((select sum(valor_aberto) from sigov.financeiro_conta_receber where tenant_id=@TenantId and status in('ABERTA','PARCIAL','VENCIDA') and not is_deleted),0) ContasReceberAberto,coalesce((select sum(valor_aberto) from sigov.financeiro_conta_pagar where tenant_id=@TenantId and status in('ABERTA','APROVADA','PARCIAL','VENCIDA') and not is_deleted),0) ContasPagarAberto,coalesce((select sum(valor) from sigov.financeiro_movimento where tenant_id=@TenantId and tipo='ENTRADA' and data_movimento::date between @Inicio and @Fim and not is_deleted),0) RecebidoPeriodo,coalesce((select sum(valor) from sigov.financeiro_movimento where tenant_id=@TenantId and tipo='SAIDA' and data_movimento::date between @Inicio and @Fim and not is_deleted),0) PagoPeriodo,(select count(*) from sigov.financeiro_conta_receber where tenant_id=@TenantId and vencimento<current_date and valor_aberto>0 and not is_deleted)+(select count(*) from sigov.financeiro_conta_pagar where tenant_id=@TenantId and vencimento<current_date and valor_aberto>0 and not is_deleted) TitulosVencidos,(select count(*) from sigov.financeiro_integracao_origem where tenant_id=@TenantId and status in('PENDENTE','ERRO')) IntegracoesPendentes", new { TenantId = tenantId, Inicio = inicio, Fim = fim }, cancellationToken: ct); return await c.QuerySingleAsync<FinanceiroDashboardDto>(cmd);
    }

    public async Task<IReadOnlyList<FluxoCaixaDto>> FluxoCaixaAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        using var c = context.CreateConnection(); var sql = @"with dias as(select generate_series(@Inicio::date,@Fim::date,'1 day')::date data),p as(select vencimento data,sum(valor_aberto) filter(where tipo='R') entradas,sum(valor_aberto) filter(where tipo='P') saidas from(select vencimento,valor_aberto,'R' tipo from sigov.financeiro_conta_receber where tenant_id=@TenantId and not is_deleted union all select vencimento,valor_aberto,'P' from sigov.financeiro_conta_pagar where tenant_id=@TenantId and not is_deleted)x group by vencimento),m as(select data_movimento::date data,sum(valor) filter(where tipo in('ENTRADA','TRANSFERENCIA_ENTRADA','ESTORNO_ENTRADA')) entradas,sum(valor) filter(where tipo in('SAIDA','TRANSFERENCIA_SAIDA','ESTORNO_SAIDA')) saidas from sigov.financeiro_movimento where tenant_id=@TenantId and not is_deleted group by 1) select d.data,coalesce(p.entradas,0) EntradasPrevistas,coalesce(p.saidas,0) SaidasPrevistas,coalesce(m.entradas,0) EntradasRealizadas,coalesce(m.saidas,0) SaidasRealizadas,coalesce(p.entradas,0)-coalesce(p.saidas,0) SaldoPrevisto,coalesce(m.entradas,0)-coalesce(m.saidas,0) SaldoRealizado from dias d left join p using(data) left join m using(data) order by d.data"; return (await c.QueryAsync<FluxoCaixaDto>(new CommandDefinition(sql, new { TenantId = tenantId, Inicio = inicio, Fim = fim }, cancellationToken: ct))).AsList();
    }

    private static async Task<decimal> AlterarSaldoAsync(IDbConnection c, IDbTransaction tx, long tenantId, long? conta, long? caixa, decimal delta, CancellationToken ct)
    {
        if (conta is not null) return await c.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("update sigov.financeiro_conta_bancaria set saldo_atual=saldo_atual+@Delta,version=version+1 where tenant_id=@TenantId and id=@Id and ativo and not is_deleted returning saldo_atual", new { TenantId = tenantId, Id = conta, Delta = delta }, tx, cancellationToken: ct)) ?? throw new InvalidOperationException("Conta bancária inexistente ou inativa.");
        return await c.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition("update sigov.financeiro_caixa set saldo_atual=saldo_atual+@Delta,version=version+1 where tenant_id=@TenantId and id=@Id and ativo and not is_deleted and exists(select 1 from sigov.financeiro_caixa_sessao s where s.tenant_id=@TenantId and s.caixa_id=@Id and s.status='ABERTA') returning saldo_atual", new { TenantId = tenantId, Id = caixa, Delta = delta }, tx, cancellationToken: ct)) ?? throw new InvalidOperationException("Caixa inexistente, inativo ou fechado.");
    }
    private static Task<long> InserirMovimentoTransferencia(IDbConnection c, IDbTransaction tx, long tenantId, long conta, string tipo, long transferencia, TransferirValoresRequest r, long? user, Guid cid, string key, CancellationToken ct) => c.QuerySingleAsync<long>(new CommandDefinition("insert into sigov.financeiro_movimento(tenant_id,conta_bancaria_id,tipo,origem,origem_id,descricao,valor,usuario_id,correlation_id,idempotency_key) values(@TenantId,@Conta,@Tipo,'TRANSFERENCIA',@Transferencia,@Descricao,@Valor,@User,@Cid,@Key) returning id", new { TenantId = tenantId, Conta = conta, Tipo = tipo, Transferencia = transferencia, r.Descricao, r.Valor, User = user, Cid = cid, Key = key }, tx, cancellationToken: ct));
    private static async Task RegistrarAsync(IDbConnection c, IDbTransaction tx, long tenantId, string evento, string agregado, long id, long? user, Guid cid, string key, object payload, CancellationToken ct) => await c.ExecuteAsync(new CommandDefinition("insert into sigov.financeiro_titulo_historico(tenant_id,tipo_titulo,titulo_id,acao,depois,usuario_id,correlation_id) values(@TenantId,@Agregado,@Id,@Evento,@Payload::jsonb,@User,@Cid); insert into sigov.outbox_evento(tenant_id,event_id,event_type,event_version,aggregate_type,aggregate_id,user_id,correlation_id,occurred_at,payload,status,attempts,next_attempt_at,idempotency_key) values(@TenantId,gen_random_uuid(),@Evento,1,@Agregado,@Id::text,@User,@Cid,now(),@Payload::jsonb,'PENDING',0,now(),@Key) on conflict(idempotency_key) do nothing", new { TenantId = tenantId, Evento = evento, Agregado = agregado, Id = id, User = user, Cid = cid, Key = key, Payload = JsonSerializer.Serialize(payload) }, tx, cancellationToken: ct));
    private sealed record TituloLock(long Id, decimal ValorAberto, string Status, long Version);
    private sealed record BaixaLock(long Id, decimal Valor, decimal Desconto, decimal Acrescimo, long MovimentoId, long? ContaId, long? CaixaId, bool Estornado);
    private sealed record ContaLock(long Id, decimal Saldo);
}
