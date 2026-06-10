using Dapper;
using Sigov.Application.Industria;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Data;
using System.Globalization;

namespace Sigov.Infrastructure.Industria;

public sealed class IndustriaEstoqueService : IIndustriaEstoqueService
{
    private readonly DapperContext _context;

    public IndustriaEstoqueService(DapperContext context) => _context = context;

    public async Task<IndustriaEstoqueResultado> ReservarMaterialAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; reserva registrada apenas na OP.");
        await GarantirSaldoAsync(connection, tenantId, produtoId, quantidade);
        await connection.ExecuteAsync("insert into sigov.comercio_estoque_saldo(tenant_id,produto_id,saldo,reservado,updated_at) values(@TenantId,@ProdutoId,0,@Quantidade,now()) on conflict(tenant_id,produto_id) do update set reservado=sigov.comercio_estoque_saldo.reservado+excluded.reservado, updated_at=now()", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade });
        await MovimentoAsync(connection, tenantId, produtoId, ordemId, "RESERVA_PRODUCAO", quantidade, correlationId);
        return new(true, true, "Material reservado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> ConsumirMaterialAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; consumo registrado sem baixa.");
        await GarantirSaldoAsync(connection, tenantId, produtoId, quantidade);
        var saldoAnterior = await SaldoAsync(connection, tenantId, produtoId);
        await connection.ExecuteAsync("update sigov.comercio_estoque_saldo set saldo=saldo-@Quantidade, reservado=greatest(reservado-@Quantidade,0), updated_at=now() where tenant_id=@TenantId and produto_id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade });
        await MovimentoAsync(connection, tenantId, produtoId, ordemId, "CONSUMO_PRODUCAO", -quantidade, correlationId, saldoAnterior, saldoAnterior - quantidade);
        return new(true, true, "Consumo baixado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> EstornarConsumoAsync(long tenantId, long ordemId, long produtoId, decimal quantidade, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; estorno sem movimento de estoque.");
        var saldoAnterior = await SaldoAsync(connection, tenantId, produtoId);
        await connection.ExecuteAsync("insert into sigov.comercio_estoque_saldo(tenant_id,produto_id,saldo,reservado,updated_at) values(@TenantId,@ProdutoId,@Quantidade,0,now()) on conflict(tenant_id,produto_id) do update set saldo=sigov.comercio_estoque_saldo.saldo+excluded.saldo, updated_at=now()", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade });
        await MovimentoAsync(connection, tenantId, produtoId, ordemId, "ESTORNO_CONSUMO_PRODUCAO", quantidade, correlationId, saldoAnterior, saldoAnterior + quantidade);
        return new(true, true, "Consumo estornado no estoque.");
    }

    public async Task<IndustriaEstoqueResultado> RegistrarProdutoAcabadoAsync(long tenantId, long ordemId, long produtoId, long? almoxarifadoId, decimal quantidade, string? lote, DateTime? validade, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, false, "Estoque e Compras não contratado; produção registrada sem entrada.");
        var saldoAnterior = await SaldoAsync(connection, tenantId, produtoId);
        await connection.ExecuteAsync("insert into sigov.comercio_estoque_saldo(tenant_id,produto_id,saldo,reservado,updated_at) values(@TenantId,@ProdutoId,@Quantidade,0,now()) on conflict(tenant_id,produto_id) do update set saldo=sigov.comercio_estoque_saldo.saldo+excluded.saldo, updated_at=now()", new { TenantId = tenantId, ProdutoId = produtoId, Quantidade = quantidade });
        await MovimentoAsync(connection, tenantId, produtoId, ordemId, "ENTRADA_PRODUCAO", quantidade, correlationId, saldoAnterior, saldoAnterior + quantidade);
        return new(true, true, "Produto acabado entrou no estoque.");
    }

    public async Task<IndustriaDisponibilidadeResultado> VerificarDisponibilidadeFichaAsync(long tenantId, long fichaTecnicaId, decimal quantidade, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        if (!await EstoqueAtivoAsync(connection, tenantId)) return new(false, true, new[] { "Estoque e Compras não contratado; disponibilidade não bloqueante." });
        var faltas = (await connection.QueryAsync<string>(@"select concat(ip.codigo, ' - saldo insuficiente') from sigov.industria_ficha_tecnica_item i join sigov.industria_ficha_tecnica f on f.id=i.ficha_tecnica_id join sigov.industria_produto ip on ip.id=i.componente_produto_id left join sigov.comercio_estoque_saldo s on s.tenant_id=f.tenant_id and s.produto_id=coalesce(ip.produto_id, ip.id) where f.id=@FichaTecnicaId and f.tenant_id=@TenantId and coalesce(s.saldo,0) < (i.quantidade*@Quantidade)", new { TenantId = tenantId, FichaTecnicaId = fichaTecnicaId, Quantidade = quantidade })).ToArray();
        return new(true, faltas.Length == 0, faltas);
    }

    public async Task<decimal> ObterCustoMedioAsync(long tenantId, long produtoId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>("select coalesce(preco_custo, preco_venda, 0) from sigov.comercio_produto where tenant_id=@TenantId and id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId }) ?? 0m;
    }

    private static async Task<bool> EstoqueAtivoAsync(IDbConnection connection, long tenantId) => await connection.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.tenant_modulo_contratado where tenant_id=@TenantId and modulo_codigo='estoque_compras' and ativo=true and status in ('CONTRATADO','HABILITADO','EM_IMPLANTACAO','BETA'))", new { TenantId = tenantId });
    private static async Task<decimal> SaldoAsync(IDbConnection connection, long tenantId, long produtoId) => await connection.ExecuteScalarAsync<decimal?>("select saldo from sigov.comercio_estoque_saldo where tenant_id=@TenantId and produto_id=@ProdutoId", new { TenantId = tenantId, ProdutoId = produtoId }) ?? 0m;
    private static async Task GarantirSaldoAsync(IDbConnection connection, long tenantId, long produtoId, decimal quantidade)
    {
        var saldo = await SaldoAsync(connection, tenantId, produtoId);
        if (saldo < quantidade) throw new InvalidOperationException("Saldo insuficiente para consumo/reserva de produção.");
    }
    private static Task MovimentoAsync(IDbConnection connection, long tenantId, long produtoId, long ordemId, string tipo, decimal quantidade, string correlationId, decimal? saldoAnterior = null, decimal? saldoPosterior = null) => connection.ExecuteAsync("insert into sigov.comercio_estoque_movimento(tenant_id,produto_id,origem,origem_id,tipo,quantidade,saldo_anterior,saldo_posterior,correlation_id) values(@TenantId,@ProdutoId,'OP',@OrdemId,@Tipo,@Quantidade,@SaldoAnterior,@SaldoPosterior,cast(@CorrelationId as uuid))", new { TenantId = tenantId, ProdutoId = produtoId, OrdemId = ordemId, Tipo = tipo, Quantidade = quantidade, SaldoAnterior = saldoAnterior, SaldoPosterior = saldoPosterior, CorrelationId = Guid.TryParse(correlationId, out var parsed) ? parsed : Guid.NewGuid() });
}

public sealed class IndustriaComercialService : IIndustriaComercialService
{
    private readonly DapperContext _context;

    public IndustriaComercialService(DapperContext context) => _context = context;

    public async Task<long> GerarOrdemProducaoDoPedidoAsync(long tenantId, long pedidoId, long? usuarioId, string correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var industriaAtiva = await connection.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.tenant_modulo_contratado where tenant_id=@TenantId and modulo_codigo='industria_producao' and ativo=true and status in ('CONTRATADO','HABILITADO','EM_IMPLANTACAO','BETA'))", new { TenantId = tenantId });
        if (!industriaAtiva) throw new InvalidOperationException("Módulo indústria e produção não contratado.");
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
