using Dapper;
using Sigov.Application.Industria;
using Sigov.Application.Saas.Modules;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Data;
using System.Globalization;

namespace Sigov.Infrastructure.Industria;

public sealed class IndustriaEstoqueService : IIndustriaEstoqueService
{
    private readonly DapperContext _context;
    private readonly IModuleEntitlementEvaluator _entitlement;

    public IndustriaEstoqueService(DapperContext context, IModuleEntitlementEvaluator entitlement)
    {
        _context = context;
        _entitlement = entitlement;
    }

    public async Task<IndustriaEstoqueResultado> ReservarMaterialAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(tenantId, ordemId, produtoId, quantidade, correlationId);
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; reserva registrada apenas na OP.");
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await SerializarOperacaoAsync(connection, transaction, tenantId, correlationId, cancellationToken);
        if (await ValidarRepeticaoAsync(connection, transaction, tenantId, ordemId, produtoId, "RESERVA_PRODUCAO", quantidade, correlationId, cancellationToken))
            return new(true, true, "Material já estava reservado para esta operação.");
        var saldo = await ObterSaldoBloqueadoAsync(connection, transaction, tenantId, produtoId, cancellationToken);
        if (saldo is null || saldo.Value.Saldo - saldo.Value.Reservado < quantidade)
            throw new InvalidOperationException("Saldo disponível insuficiente para reserva de produção.");
        await connection.ExecuteAsync(new CommandDefinition("update sigov.comercio_estoque_saldo set reservado=reservado+@Quantidade, updated_at=now() where tenant_id=@TenantId and produto_id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade }, transaction, cancellationToken: cancellationToken));
        await MovimentoAsync(connection, transaction, tenantId, produtoId, ordemId, "RESERVA_PRODUCAO", quantidade, correlationId, saldo.Value.Saldo, saldo.Value.Saldo, cancellationToken);
        transaction.Commit();
        return new(true, true, "Material reservado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> ConsumirMaterialAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(tenantId, ordemId, produtoId, quantidade, correlationId);
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; consumo registrado sem baixa.");
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await SerializarOperacaoAsync(connection, transaction, tenantId, correlationId, cancellationToken);
        if (await ValidarRepeticaoAsync(connection, transaction, tenantId, ordemId, produtoId, "CONSUMO_PRODUCAO", -quantidade, correlationId, cancellationToken))
            return new(true, true, "Consumo já registrado para esta operação.");
        var saldo = await ObterSaldoBloqueadoAsync(connection, transaction, tenantId, produtoId, cancellationToken);
        if (saldo is null || saldo.Value.Saldo < quantidade)
            throw new InvalidOperationException("Saldo insuficiente para consumo de produção.");
        var reservaConsumida = Math.Min(saldo.Value.Reservado, quantidade);
        var afetados = await connection.ExecuteAsync(new CommandDefinition("update sigov.comercio_estoque_saldo set saldo=saldo-@Quantidade, reservado=reservado-@ReservaConsumida, updated_at=now() where tenant_id=@TenantId and produto_id=@ProdutoId and saldo>=@Quantidade and reservado>=@ReservaConsumida", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade, ReservaConsumida = reservaConsumida }, transaction, cancellationToken: cancellationToken));
        if (afetados != 1) throw new InvalidOperationException("O saldo foi alterado concorrentemente; refaça a operação.");
        await MovimentoAsync(connection, transaction, tenantId, produtoId, ordemId, "CONSUMO_PRODUCAO", -quantidade, correlationId, saldo.Value.Saldo, saldo.Value.Saldo - quantidade, cancellationToken);
        transaction.Commit();
        return new(true, true, "Consumo baixado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> EstornarConsumoAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(tenantId, ordemId, produtoId, quantidade, correlationId);
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; estorno sem movimento de estoque.");
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await SerializarOperacaoAsync(connection, transaction, tenantId, correlationId, cancellationToken);
        if (await ValidarRepeticaoAsync(connection, transaction, tenantId, ordemId, produtoId, "ESTORNO_CONSUMO_PRODUCAO", quantidade, correlationId, cancellationToken))
            return new(true, true, "Estorno já registrado para esta operação.");
        var consumido = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("select coalesce(-sum(quantidade),0) from sigov.comercio_estoque_movimento where tenant_id=@TenantId and produto_id=@ProdutoId and origem='OP' and origem_id=@OrdemId and tipo='CONSUMO_PRODUCAO'", new { TenantId = tenantId, ProdutoId = produtoId, OrdemId = ordemId }, transaction, cancellationToken: cancellationToken));
        var estornado = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("select coalesce(sum(quantidade),0) from sigov.comercio_estoque_movimento where tenant_id=@TenantId and produto_id=@ProdutoId and origem='OP' and origem_id=@OrdemId and tipo='ESTORNO_CONSUMO_PRODUCAO'", new { TenantId = tenantId, ProdutoId = produtoId, OrdemId = ordemId }, transaction, cancellationToken: cancellationToken));
        if (quantidade > consumido - estornado) throw new InvalidOperationException("Quantidade de estorno excede o consumo ainda estornável.");
        var saldo = await ObterSaldoBloqueadoAsync(connection, transaction, tenantId, produtoId, cancellationToken) ?? throw new InvalidOperationException("Saldo de estoque não encontrado.");
        await connection.ExecuteAsync(new CommandDefinition("update sigov.comercio_estoque_saldo set saldo=saldo+@Quantidade, updated_at=now() where tenant_id=@TenantId and produto_id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade }, transaction, cancellationToken: cancellationToken));
        await MovimentoAsync(connection, transaction, tenantId, produtoId, ordemId, "ESTORNO_CONSUMO_PRODUCAO", quantidade, correlationId, saldo.Saldo, saldo.Saldo + quantidade, cancellationToken);
        transaction.Commit();
        return new(true, true, "Consumo estornado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> RegistrarProdutoAcabadoAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, string? lote, DateTime? validade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(tenantId, ordemId, produtoId, quantidade, correlationId);
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; produção registrada sem entrada.");
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        await SerializarOperacaoAsync(connection, transaction, tenantId, correlationId, cancellationToken);
        if (await ValidarRepeticaoAsync(connection, transaction, tenantId, ordemId, produtoId, "ENTRADA_PRODUCAO", quantidade, correlationId, cancellationToken))
            return new(true, true, "Entrada de produto acabado já registrada para esta operação.");
        await connection.ExecuteAsync(new CommandDefinition("insert into sigov.comercio_estoque_saldo(tenant_id,produto_id,saldo,reservado,updated_at) values(@TenantId,@ProdutoId,0,0,now()) on conflict(tenant_id,produto_id) do nothing", new { TenantId = tenantId, ProdutoId = produtoId }, transaction, cancellationToken: cancellationToken));
        var saldo = await ObterSaldoBloqueadoAsync(connection, transaction, tenantId, produtoId, cancellationToken) ?? throw new InvalidOperationException("Não foi possível inicializar o saldo.");
        await connection.ExecuteAsync(new CommandDefinition("update sigov.comercio_estoque_saldo set saldo=saldo+@Quantidade, updated_at=now() where tenant_id=@TenantId and produto_id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade }, transaction, cancellationToken: cancellationToken));
        await MovimentoAsync(connection, transaction, tenantId, produtoId, ordemId, "ENTRADA_PRODUCAO", quantidade, correlationId, saldo.Saldo, saldo.Saldo + quantidade, cancellationToken);
        transaction.Commit();
        return new(true, true, "Produto acabado entrou no estoque.");
    }

    public async Task<IndustriaDisponibilidadeResultado> VerificarDisponibilidadeFichaAsync(long tenantId, long fichaTecnicaId, decimal quantidade, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, true, new[] { "Estoque e Compras não contratado; disponibilidade não bloqueante." });
        var faltas = (await connection.QueryAsync<string>(@"select concat(ip.codigo, ' - saldo disponível insuficiente') from sigov.industria_ficha_tecnica_item i join sigov.industria_ficha_tecnica f on f.id=i.ficha_tecnica_id join sigov.industria_produto ip on ip.id=i.componente_produto_id left join sigov.comercio_estoque_saldo s on s.tenant_id=f.tenant_id and s.produto_id=coalesce(ip.produto_id, ip.id) where f.id=@FichaTecnicaId and f.tenant_id=@TenantId and coalesce(s.saldo-s.reservado,0) < (i.quantidade*@Quantidade)", new { TenantId = tenantId, FichaTecnicaId = fichaTecnicaId, Quantidade = quantidade })).ToArray();
        return new(true, faltas.Length == 0, faltas);
    }

    public async Task<decimal> ObterCustoMedioAsync(long tenantId, long produtoId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>("select preco_custo from sigov.comercio_produto where tenant_id=@TenantId and id=@ProdutoId and preco_custo is not null", new { TenantId = tenantId, ProdutoId = produtoId })
            ?? throw new InvalidOperationException("Custo cadastral do produto não informado; preço de venda não pode ser usado como custo.");
    }

    private async Task<bool> EstoqueAtivoAsync(IDbConnection connection, long tenantId)
    {
        _ = connection;
        var decision = await _entitlement.EvaluateAsync(new ModuleEntitlementRequest(0, "estoque_compras", Array.Empty<string>(), tenantId)).ConfigureAwait(false);
        return decision.Allowed;
    }
    private static void ValidarOperacao(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId)
    {
        if (tenantId <= 0 || ordemId <= 0 || produtoId <= 0) throw new ArgumentException("Tenant, ordem e produto válidos são obrigatórios.");
        if (quantidade <= 0) throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        if (!Guid.TryParse(correlationId, out _)) throw new ArgumentException("Identificador da operação inválido.", nameof(correlationId));
    }

    private static Task SerializarOperacaoAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, string correlationId, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(hashtextextended(@Chave, @TenantId))", new { Chave = correlationId, TenantId = tenantId }, transaction, cancellationToken: cancellationToken));

    private static async Task<bool> ValidarRepeticaoAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, long ordemId, long produtoId, string tipo, decimal quantidade, string correlationId, CancellationToken cancellationToken)
    {
        var repeticao = await connection.QuerySingleAsync<(bool Existe, bool MesmoConteudo)>(new CommandDefinition(@"select
exists(select 1 from sigov.comercio_estoque_movimento where tenant_id=@TenantId and tipo=@Tipo and correlation_id=cast(@CorrelationId as uuid)) as Existe,
exists(select 1 from sigov.comercio_estoque_movimento where tenant_id=@TenantId and tipo=@Tipo and correlation_id=cast(@CorrelationId as uuid) and produto_id=@ProdutoId and origem='OP' and origem_id=@OrdemId and quantidade=@Quantidade) as MesmoConteudo", new { TenantId = tenantId, OrdemId = ordemId, ProdutoId = produtoId, Tipo = tipo, Quantidade = quantidade, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken));
        if (repeticao.Existe && !repeticao.MesmoConteudo)
            throw new InvalidOperationException("A chave da operação já foi utilizada com conteúdo diferente.");
        return repeticao.MesmoConteudo;
    }

    private static Task<(decimal Saldo, decimal Reservado)?> ObterSaldoBloqueadoAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, long produtoId, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<(decimal Saldo, decimal Reservado)?>(new CommandDefinition("select saldo, reservado from sigov.comercio_estoque_saldo where tenant_id=@TenantId and produto_id=@ProdutoId for update", new { TenantId = tenantId, ProdutoId = produtoId }, transaction, cancellationToken: cancellationToken));

    private static Task MovimentoAsync(IDbConnection connection, IDbTransaction transaction, long tenantId, long produtoId, long ordemId, string tipo, decimal quantidade, string correlationId, decimal? saldoAnterior, decimal? saldoPosterior, CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition("insert into sigov.comercio_estoque_movimento(tenant_id,produto_id,origem,origem_id,tipo,quantidade,saldo_anterior,saldo_posterior,correlation_id) values(@TenantId,@ProdutoId,'OP',@OrdemId,@Tipo,@Quantidade,@SaldoAnterior,@SaldoPosterior,cast(@CorrelationId as uuid))", new { TenantId = tenantId, ProdutoId = produtoId, OrdemId = ordemId, Tipo = tipo, Quantidade = quantidade, SaldoAnterior = saldoAnterior, SaldoPosterior = saldoPosterior, CorrelationId = correlationId }, transaction, cancellationToken: cancellationToken));
}

public sealed class IndustriaComercialService : IIndustriaComercialService
{
    private readonly DapperContext _context;
    private readonly IModuleEntitlementEvaluator _entitlement;

    public IndustriaComercialService(DapperContext context, IModuleEntitlementEvaluator entitlement)
    {
        _context = context;
        _entitlement = entitlement;
    }

    public async Task<long> GerarOrdemProducaoDoPedidoAsync(long tenantId, long pedidoId, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var industriaAtiva = await _entitlement.EvaluateAsync(new ModuleEntitlementRequest(
            usuarioId ?? 0, "industria_producao", Array.Empty<string>(), tenantId, CorrelationId: correlationId), cancellationToken).ConfigureAwait(false);
        if (!industriaAtiva.Allowed) throw new InvalidOperationException(industriaAtiva.Reason);
        var item = await connection.QuerySingleOrDefaultAsync<dynamic>(@"select pi.produto_id, pi.quantidade, p.codigo, p.nome from sigov.comercio_pedido_item pi join sigov.comercio_pedido ped on ped.id=pi.pedido_id and ped.tenant_id=@TenantId join sigov.comercio_produto p on p.id=pi.produto_id where pi.pedido_id=@PedidoId order by pi.id limit 1", new { TenantId = tenantId, PedidoId = pedidoId });
        if (item is null) throw new InvalidOperationException("Pedido sem item para gerar OP.");
        var produtoIndustrialId = await connection.ExecuteScalarAsync<long?>("select id from sigov.industria_produto where tenant_id=@TenantId and (produto_id=@ProdutoId or codigo=@Codigo) and ativo=true order by id limit 1", new { TenantId = tenantId, ProdutoId = (long)item.produto_id, Codigo = (string)item.codigo });
        if (!produtoIndustrialId.HasValue) throw new InvalidOperationException("Produto industrial não configurado para o item vendido.");
        var numero = $"OP-PED-{pedidoId.ToString(CultureInfo.InvariantCulture)}";
        var ordemId = await connection.ExecuteScalarAsync<long>(@"insert into sigov.industria_ordem_producao(tenant_id,numero,produto_id,pedido_id,quantidade_planejada,observacao) values(@TenantId,@Numero,@ProdutoId,@PedidoId,@Quantidade,'Gerada a partir de pedido comercial') on conflict(tenant_id,numero) do update set pedido_id=excluded.pedido_id returning id", new { TenantId = tenantId, Numero = numero, ProdutoId = produtoIndustrialId.Value, PedidoId = pedidoId, Quantidade = (decimal)item.quantidade });
        await connection.ExecuteAsync("insert into sigov.industria_ordem_historico(tenant_id,ordem_id,status_novo,usuario_id,origem,observacao,correlation_id) values(@TenantId,@OrdemId,'PLANEJADA',@UsuarioId,'COMERCIAL','PEDIDO_GEROU_OP',cast(@CorrelationId as uuid))", new { TenantId = tenantId, OrdemId = ordemId, UsuarioId = usuarioId, CorrelationId = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid() });
        await connection.ExecuteAsync("insert into sigov.auditoria_evento(tenant_id,usuario_id,acao,entidade,entidade_id,correlation_id,depois,created_at) values(@TenantId,@UsuarioId,'PEDIDO_GEROU_OP','industria_ordem_producao',@EntidadeId,cast(@CorrelationId as uuid),jsonb_build_object('pedido_id',@PedidoId,'ordem_id',@OrdemId),now())", new { TenantId = tenantId, UsuarioId = usuarioId, EntidadeId = ordemId.ToString(CultureInfo.InvariantCulture), PedidoId = pedidoId, OrdemId = ordemId, CorrelationId = Guid.TryParse(correlationId, out var audit) ? audit : Guid.NewGuid() });
        return ordemId;
    }
}
