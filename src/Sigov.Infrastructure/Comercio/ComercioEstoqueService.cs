using Dapper;
using Sigov.Application.Comercio;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Comercio;

public sealed class ComercioEstoqueService : IComercioEstoqueService
{
    private readonly DapperContext _context;

    public ComercioEstoqueService(DapperContext context) => _context = context;

    public async Task ReservarEstoqueAsync(long tenantId, long origemId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken)
    {
        foreach (var item in itens)
        {
            await AplicarAsync(tenantId, item.ProdutoId, origemId, "PEDIDO", item.OrigemId, "RESERVA_PEDIDO", 0, item.Quantidade, cancellationToken);
        }
    }

    public async Task BaixarEstoqueVendaAsync(long tenantId, long vendaId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken)
    {
        foreach (var item in itens)
        {
            await AplicarAsync(tenantId, item.ProdutoId, vendaId, "VENDA", item.OrigemId, "VENDA", -item.Quantidade, 0, cancellationToken);
        }
    }

    public async Task EstornarEstoqueVendaAsync(long tenantId, long vendaId, IReadOnlyCollection<ComercioEstoqueItem> itens, CancellationToken cancellationToken)
    {
        foreach (var item in itens)
        {
            await AplicarAsync(tenantId, item.ProdutoId, vendaId, "VENDA", item.OrigemId, "CANCELAMENTO_VENDA", item.Quantidade, 0, cancellationToken);
        }
    }

    public async Task<bool> VerificarDisponibilidadeAsync(long tenantId, long produtoId, decimal quantidade, bool permiteNegativo, CancellationToken cancellationToken)
    {
        if (permiteNegativo)
        {
            return true;
        }

        var saldo = await ObterSaldoProdutoAsync(tenantId, produtoId, cancellationToken);
        return saldo >= quantidade;
    }

    public async Task<decimal> ObterSaldoProdutoAsync(long tenantId, long produtoId, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("select coalesce((select saldo - reservado from sigov.comercio_estoque_saldo where tenant_id=@TenantId and produto_id=@ProdutoId),0)", new { TenantId = tenantId, ProdutoId = produtoId }, cancellationToken: cancellationToken));
    }

    private async Task AplicarAsync(long tenantId, long produtoId, long origemId, string origem, long itemOrigemId, string tipo, decimal saldoDelta, decimal reservadoDelta, CancellationToken cancellationToken)
    {
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(@"
insert into sigov.comercio_estoque_saldo(tenant_id,produto_id,saldo,reservado,updated_at)
values(@TenantId,@ProdutoId,0,0,now())
on conflict(tenant_id,produto_id) do nothing;
with anterior as (select saldo, reservado from sigov.comercio_estoque_saldo where tenant_id=@TenantId and produto_id=@ProdutoId),
atualizado as (
    update sigov.comercio_estoque_saldo
       set saldo = saldo + @SaldoDelta,
           reservado = greatest(0, reservado + @ReservadoDelta),
           updated_at = now()
     where tenant_id=@TenantId and produto_id=@ProdutoId
 returning saldo
)
insert into sigov.comercio_estoque_movimento(tenant_id,produto_id,origem,origem_id,tipo,quantidade,saldo_anterior,saldo_posterior)
select @TenantId,@ProdutoId,@Origem,@OrigemId,@Tipo,@Quantidade,anterior.saldo,atualizado.saldo
from anterior, atualizado;", new { TenantId = tenantId, ProdutoId = produtoId, Origem = origem, OrigemId = origemId, Tipo = tipo, Quantidade = Math.Abs(saldoDelta) + Math.Abs(reservadoDelta), SaldoDelta = saldoDelta, ReservadoDelta = reservadoDelta }, cancellationToken: cancellationToken));
    }
}
