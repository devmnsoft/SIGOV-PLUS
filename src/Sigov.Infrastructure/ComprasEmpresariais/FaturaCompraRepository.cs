using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.ComprasEmpresariais;

public sealed class FaturaCompraRepository(NpgsqlConnectionFactory factory) : IFaturaCompraRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static string Sha256(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    private static Task LockPedidoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenantId, Guid pedidoId, CancellationToken ct) =>
        cn.ExecuteAsync(new CommandDefinition(
            "select pg_advisory_xact_lock(hashtextextended(@k, 0))",
            new { k = $"{tenantId:D}|FATURA_PEDIDO|{pedidoId:D}" },
            tx,
            cancellationToken: ct));

    private static Task LockFaturaAsync(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenantId, Guid faturaId, CancellationToken ct) =>
        cn.ExecuteAsync(new CommandDefinition(
            "select pg_advisory_xact_lock(hashtextextended(@k, 0))",
            new { k = $"{tenantId:D}|FATURA|{faturaId:D}" },
            tx,
            cancellationToken: ct));

    public async Task<PagedResult<FaturaResumo>> ListarAsync(ComprasContext x, FaturaFiltro f, CancellationToken ct)
    {
        var page = Math.Max(1, f.Pagina);
        var size = Math.Clamp(f.Tamanho, 1, 100);
        var off = (long)(page - 1) * size;

        const string sqlCount = @"
select count(*)
from sigov.compras_empresarial_fatura f
join sigov.compras_empresarial_fornecedor fn on fn.id = f.fornecedor_id and fn.tenant_id = f.tenant_id
join sigov.compras_empresarial_pedido p on p.id = f.pedido_id and p.tenant_id = f.tenant_id
where f.tenant_id = @t
  and (@st is null or f.status = @st)
  and (@forn is null or f.fornecedor_id = @forn)
  and (@ped is null or f.pedido_id = @ped)
  and (@busca is null or f.numero ilike '%'||@busca||'%' or f.chave_acesso ilike '%'||@busca||'%' or fn.razao_social ilike '%'||@busca||'%' or p.numero ilike '%'||@busca||'%');";

        const string sqlData = @"
select
    f.id as Id,
    f.numero as Numero,
    f.serie as Serie,
    f.tipo_documento as TipoDocumento,
    f.fornecedor_id as FornecedorId,
    fn.razao_social as FornecedorNome,
    f.pedido_id as PedidoId,
    p.numero as PedidoNumero,
    f.total as Total,
    f.valor_liquido as ValorLiquido,
    f.status as Status,
    coalesce(f.resultado_match, 'EM_CONFERENCIA') as ResultadoMatch,
    f.created_at as CriadaEm,
    f.data_emissao as DataEmissao,
    f.data_vencimento as DataVencimento,
    (select count(*) from sigov.compras_empresarial_fatura_item fi where fi.fatura_id = f.id and fi.tenant_id = f.tenant_id)::int as ItensCount,
    f.version as Version
from sigov.compras_empresarial_fatura f
join sigov.compras_empresarial_fornecedor fn on fn.id = f.fornecedor_id and fn.tenant_id = f.tenant_id
join sigov.compras_empresarial_pedido p on p.id = f.pedido_id and p.tenant_id = f.tenant_id
where f.tenant_id = @t
  and (@st is null or f.status = @st)
  and (@forn is null or f.fornecedor_id = @forn)
  and (@ped is null or f.pedido_id = @ped)
  and (@busca is null or f.numero ilike '%'||@busca||'%' or f.chave_acesso ilike '%'||@busca||'%' or fn.razao_social ilike '%'||@busca||'%' or p.numero ilike '%'||@busca||'%')
order by f.created_at desc, f.id desc
offset @off limit @s;";

        await using var cn = factory.CreateConnection();
        var prms = new { t = x.TenantId, st = f.Status, forn = f.FornecedorId, ped = f.PedidoId, busca = f.Busca, off, s = size };
        using var m = await cn.QueryMultipleAsync(new CommandDefinition($"{sqlCount}\n{sqlData}", prms, cancellationToken: ct));
        var total = await m.ReadSingleAsync<long>();
        var items = (await m.ReadAsync<FaturaResumo>()).AsList();
        return new PagedResult<FaturaResumo>(items, page, size, total);
    }

    public async Task<FaturaDetalhe?> ObterAsync(ComprasContext x, Guid id, CancellationToken ct)
    {
        const string sqlHeader = @"
select
    f.id as Id,
    f.tenant_id as TenantId,
    f.numero as Numero,
    f.serie as Serie,
    f.tipo_documento as TipoDocumento,
    f.chave_acesso as ChaveAcesso,
    f.fornecedor_id as FornecedorId,
    fn.razao_social as FornecedorNome,
    fn.documento as FornecedorCnpj,
    f.pedido_id as PedidoId,
    p.numero as PedidoNumero,
    f.total as Total,
    f.valor_itens as ValorItens,
    f.valor_desconto as ValorDesconto,
    f.valor_frete as ValorFrete,
    f.valor_seguro as ValorSeguro,
    f.valor_outras_despesas as ValorOutrasDespesas,
    f.valor_liquido as ValorLiquido,
    f.status as Status,
    coalesce(f.resultado_match, 'EM_CONFERENCIA') as ResultadoMatch,
    f.data_emissao as DataEmissao,
    f.data_vencimento as DataVencimento,
    f.observacoes as Observacoes,
    f.justificativa as Justificativa,
    coalesce(u.nome, f.decidido_por) as DecididoPor,
    f.decidido_em as DecididoEm,
    f.motivo_rejeicao as MotivoRejeicao,
    f.created_at as CriadaEm,
    f.version as Version
from sigov.compras_empresarial_fatura f
join sigov.compras_empresarial_fornecedor fn on fn.id = f.fornecedor_id and fn.tenant_id = f.tenant_id
join sigov.compras_empresarial_pedido p on p.id = f.pedido_id and p.tenant_id = f.tenant_id
left join sigov.usuario u on md5('sigov:usuario:'||u.id::text)::uuid = nullif(f.decidido_por, '')::uuid
where f.id = @id and f.tenant_id = @t;";

        const string sqlItens = @"
select
    fi.id as Id,
    fi.pedido_item_id as PedidoItemId,
    coalesce(ci.descricao, ep.nome, 'Item '||fi.pedido_item_id::text) as ProdutoNome,
    coalesce(ci.unidade_medida, ep.unidade_medida, 'UN') as Unidade,
    pi.quantidade as QuantidadePedida,
    pi.preco_unitario as PrecoUnitarioPedido,
    fi.quantidade as QuantidadeFaturada,
    fi.valor_unitario as PrecoUnitarioFatura,
    fi.valor_total as TotalItem,
    coalesce((
        select sum(ri.quantidade_aceita)
        from sigov.compras_empresarial_recebimento_item ri
        join sigov.compras_empresarial_recebimento r on r.id = ri.recebimento_id and r.tenant_id = ri.tenant_id
        where ri.tenant_id = fi.tenant_id
          and ri.pedido_item_id = fi.pedido_item_id
          and r.status <> 'CANCELADO'
    ), 0) as QuantidadeAceitaTotal,
    coalesce((
        select sum(ofi.quantidade)
        from sigov.compras_empresarial_fatura_item ofi
        join sigov.compras_empresarial_fatura of on of.id = ofi.fatura_id and of.tenant_id = ofi.tenant_id
        where ofi.tenant_id = fi.tenant_id
          and ofi.pedido_item_id = fi.pedido_item_id
          and of.id <> fi.fatura_id
          and of.status in ('APROVADA', 'EM_CONFERENCIA', 'CONFORME', 'COM_DIVERGENCIA')
    ), 0) as QuantidadeFaturadaOutras
from sigov.compras_empresarial_fatura_item fi
join sigov.compras_empresarial_pedido_item pi on pi.id = fi.pedido_item_id and pi.tenant_id = fi.tenant_id
left join sigov.catalogo_item ci on ci.id = pi.catalogo_item_id
left join sigov.estoque_produto ep on ep.id = pi.produto_id
where fi.fatura_id = @id and fi.tenant_id = @t
order by fi.id asc;";

        const string sqlEventos = @"
select
    fe.id as Id,
    fe.tipo as Tipo,
    fe.detalhes::text as Detalhes,
    fe.usuario_id as UsuarioId,
    coalesce(u.nome, fe.usuario_id::text) as Autor,
    fe.ocorrido_em as OcorridoEm,
    fe.correlation_id as CorrelationId
from sigov.compras_empresarial_fatura_evento fe
left join sigov.usuario u on md5('sigov:usuario:'||u.id::text)::uuid = fe.usuario_id
where fe.fatura_id = @id and fe.tenant_id = @t
order by fe.ocorrido_em desc, fe.id desc;";

        await using var cn = factory.CreateConnection();
        var prms = new { id, t = x.TenantId };
        var head = await cn.QuerySingleOrDefaultAsync<FaturaHeaderDto>(new CommandDefinition(sqlHeader, prms, cancellationToken: ct));
        if (head is null) return null;

        var rawItens = (await cn.QueryAsync<FaturaItemRawDto>(new CommandDefinition(sqlItens, prms, cancellationToken: ct))).AsList();
        var rawEventos = (await cn.QueryAsync<FaturaEventoRawDto>(new CommandDefinition(sqlEventos, prms, cancellationToken: ct))).AsList();

        var itens = new List<FaturaItemDetalhe>(rawItens.Count);
        foreach (var r in rawItens)
        {
            var saldoElegivel = Math.Max(0m, Math.Min(r.QuantidadePedida, r.QuantidadeAceitaTotal) - r.QuantidadeFaturadaOutras);
            var difQtd = r.QuantidadeFaturada > saldoElegivel;
            var difPreco = r.PrecoUnitarioFatura != r.PrecoUnitarioPedido;

            string statusItem;
            string diagnostico;
            if (difQtd && difPreco)
            {
                statusItem = "DIVERGENTE_MISTO";
                diagnostico = $"Qtd faturada ({r.QuantidadeFaturada:0.####}) excede saldo elegível ({saldoElegivel:0.####}) e preço (R$ {r.PrecoUnitarioFatura:0.00}) diverge do pedido (R$ {r.PrecoUnitarioPedido:0.00}).";
            }
            else if (difQtd)
            {
                statusItem = "DIVERGENTE_QUANTIDADE";
                diagnostico = $"Qtd faturada ({r.QuantidadeFaturada:0.####}) excede saldo elegível ({saldoElegivel:0.####}).";
            }
            else if (difPreco)
            {
                statusItem = "DIVERGENTE_VALOR";
                diagnostico = $"Preço faturado (R$ {r.PrecoUnitarioFatura:0.00}) diverge do preço homologado (R$ {r.PrecoUnitarioPedido:0.00}).";
            }
            else
            {
                statusItem = "CONFORME";
                diagnostico = "Item em perfeita conformidade com pedido e recebimentos aceitos.";
            }

            itens.Add(new FaturaItemDetalhe(
                r.Id,
                r.PedidoItemId,
                r.ProdutoNome,
                r.Unidade,
                r.QuantidadePedida,
                r.QuantidadeAceitaTotal,
                r.QuantidadeFaturadaOutras,
                saldoElegivel,
                r.QuantidadeFaturada,
                r.PrecoUnitarioPedido,
                r.PrecoUnitarioFatura,
                r.TotalItem,
                statusItem,
                diagnostico));
        }

        var eventos = rawEventos.Select(e => new FaturaEventoDetalhe(
            e.Id,
            e.Tipo,
            e.Detalhes,
            e.UsuarioId,
            e.Autor,
            e.OcorridoEm,
            e.CorrelationId)).ToList();

        return new FaturaDetalhe(
            head.Id,
            head.TenantId,
            head.Numero,
            head.Serie,
            head.TipoDocumento,
            head.ChaveAcesso,
            head.FornecedorId,
            head.FornecedorNome,
            head.FornecedorCnpj,
            head.PedidoId,
            head.PedidoNumero,
            head.Total,
            head.ValorItens,
            head.ValorDesconto,
            head.ValorFrete,
            head.ValorSeguro,
            head.ValorOutrasDespesas,
            head.ValorLiquido,
            head.Status,
            head.ResultadoMatch,
            head.DataEmissao,
            head.DataVencimento,
            head.Observacoes,
            head.Justificativa,
            head.DecididoPor,
            head.DecididoEm,
            head.MotivoRejeicao,
            head.CriadaEm,
            head.Version,
            itens,
            eventos);
    }

    public async Task<FaturaConferenciaPrevia> ObterConferenciaPreviaAsync(ComprasContext x, Guid pedidoId, CancellationToken ct)
    {
        const string sqlPedido = @"
select
    p.id as PedidoId,
    p.numero as PedidoNumero,
    p.fornecedor_id as FornecedorId,
    fn.razao_social as FornecedorNome,
    fn.documento as FornecedorCnpj,
    p.total as TotalPedido
from sigov.compras_empresarial_pedido p
join sigov.compras_empresarial_fornecedor fn on fn.id = p.fornecedor_id and fn.tenant_id = p.tenant_id
where p.id = @pedidoId and p.tenant_id = @t and not p.is_deleted;";

        const string sqlItens = @"
select
    pi.id as PedidoItemId,
    coalesce(ci.descricao, ep.nome, 'Item '||pi.id::text) as ProdutoNome,
    coalesce(ci.unidade_medida, ep.unidade_medida, 'UN') as Unidade,
    pi.quantidade as QuantidadePedida,
    pi.preco_unitario as ValorUnitarioPedido,
    coalesce((
        select sum(ri.quantidade_aceita)
        from sigov.compras_empresarial_recebimento_item ri
        join sigov.compras_empresarial_recebimento r on r.id = ri.recebimento_id and r.tenant_id = ri.tenant_id
        where ri.tenant_id = pi.tenant_id
          and ri.pedido_item_id = pi.id
          and r.status <> 'CANCELADO'
    ), 0) as QuantidadeAceitaTotal,
    coalesce((
        select sum(fi.quantidade)
        from sigov.compras_empresarial_fatura_item fi
        join sigov.compras_empresarial_fatura f on f.id = fi.fatura_id and f.tenant_id = fi.tenant_id
        where fi.tenant_id = pi.tenant_id
          and fi.pedido_item_id = pi.id
          and f.status in ('APROVADA', 'EM_CONFERENCIA', 'CONFORME', 'COM_DIVERGENCIA')
    ), 0) as QuantidadeFaturadaOutras
from sigov.compras_empresarial_pedido_item pi
left join sigov.catalogo_item ci on ci.id = pi.catalogo_item_id
left join sigov.estoque_produto ep on ep.id = pi.produto_id
where pi.pedido_id = @pedidoId and pi.tenant_id = @t
order by pi.id asc;";

        await using var cn = factory.CreateConnection();
        var prms = new { pedidoId, t = x.TenantId };
        var p = await cn.QuerySingleOrDefaultAsync<PreviaPedidoDto>(new CommandDefinition(sqlPedido, prms, cancellationToken: ct));
        if (p is null)
            throw new InvalidOperationException("Pedido não encontrado ou não pertence a esta entidade.");

        var raw = (await cn.QueryAsync<PreviaItemRawDto>(new CommandDefinition(sqlItens, prms, cancellationToken: ct))).AsList();
        var itens = raw.Select(i =>
        {
            var saldoElegivel = Math.Max(0m, Math.Min(i.QuantidadePedida, i.QuantidadeAceitaTotal) - i.QuantidadeFaturadaOutras);
            return new FaturaConferenciaPreviaItem(
                i.PedidoItemId,
                i.ProdutoNome,
                i.Unidade,
                i.QuantidadePedida,
                i.QuantidadeAceitaTotal,
                i.QuantidadeFaturadaOutras,
                saldoElegivel,
                i.ValorUnitarioPedido);
        }).ToList();

        return new FaturaConferenciaPrevia(p.PedidoId, p.PedidoNumero, p.FornecedorId, p.FornecedorNome, p.FornecedorCnpj, p.TotalPedido, itens);
    }

    public async Task<FaturaComandoResultado> CriarAsync(ComprasContext x, CriarFaturaRequest r, string key, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);

        var requestHash = Sha256(JsonSerializer.Serialize(r, JsonOpts));

        // 1. Idempotência
        var idempotencia = await cn.QuerySingleOrDefaultAsync<IdempotenciaDto>(new CommandDefinition(
            @"select fatura_id as FaturaId, resultado as ResultadoJson, request_hash as RequestHash
              from sigov.compras_empresarial_fatura_idempotencia
              where tenant_id = @t and operacao = 'CRIAR_FATURA' and chave = @k",
            new { t = x.TenantId, k = key },
            tx,
            cancellationToken: ct));

        if (idempotencia is not null)
        {
            if (idempotencia.RequestHash != requestHash)
                throw new InvalidOperationException("A chave de idempotência já foi utilizada com uma carga de dados diferente.");

            if (!string.IsNullOrWhiteSpace(idempotencia.ResultadoJson))
            {
                var prevRes = JsonSerializer.Deserialize<FaturaComandoResultado>(idempotencia.ResultadoJson, JsonOpts);
                if (prevRes is not null) return prevRes with { Repetido = true };
            }
        }

        // 2. Lock no pedido
        await LockPedidoAsync(cn, tx, x.TenantId, r.PedidoId, ct);

        // 3. Validação do pedido e fornecedor
        var pedido = await cn.QuerySingleOrDefaultAsync<(Guid FornecedorId, string Status)>(new CommandDefinition(
            @"select fornecedor_id, status from sigov.compras_empresarial_pedido
              where id = @id and tenant_id = @t and not is_deleted",
            new { id = r.PedidoId, t = x.TenantId },
            tx,
            cancellationToken: ct));

        if (pedido == default)
            throw new InvalidOperationException("Pedido inexistente ou excluído.");

        if (pedido.FornecedorId != r.FornecedorId)
            throw new InvalidOperationException("O fornecedor informado diverge do fornecedor homologado no pedido.");

        if (pedido.Status is "CANCELADO" or "RASCUNHO")
            throw new InvalidOperationException($"Não é permitido registrar faturas para pedidos com status '{pedido.Status}'.");

        // 4. Unicidade de número/série e chave de acesso
        var duplicada = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"select count(*) from sigov.compras_empresarial_fatura
              where tenant_id = @t and fornecedor_id = @forn and numero = @num and serie = @ser",
            new { t = x.TenantId, forn = r.FornecedorId, num = r.Numero, ser = r.Serie },
            tx,
            cancellationToken: ct));

        if (duplicada > 0)
            throw new InvalidOperationException($"Já existe uma fatura cadastrada com o número '{r.Numero}' e série '{r.Serie}' para este fornecedor.");

        if (!string.IsNullOrWhiteSpace(r.ChaveAcesso))
        {
            var chaveDuplicada = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                @"select count(*) from sigov.compras_empresarial_fatura
                  where tenant_id = @t and chave_acesso = @chave",
                new { t = x.TenantId, chave = r.ChaveAcesso },
                tx,
                cancellationToken: ct));

            if (chaveDuplicada > 0)
                throw new InvalidOperationException($"A chave de acesso '{r.ChaveAcesso}' já está cadastrada para outra fatura neste órgão.");
        }

        // 5. Carregar itens do pedido e saldos para conferência trilateral
        const string sqlItensPedido = @"
select
    pi.id as PedidoItemId,
    pi.quantidade as QuantidadePedida,
    pi.preco_unitario as PrecoUnitarioPedido,
    coalesce((
        select sum(ri.quantidade_aceita)
        from sigov.compras_empresarial_recebimento_item ri
        join sigov.compras_empresarial_recebimento r on r.id = ri.recebimento_id and r.tenant_id = ri.tenant_id
        where ri.tenant_id = pi.tenant_id
          and ri.pedido_item_id = pi.id
          and r.status <> 'CANCELADO'
    ), 0) as QuantidadeAceitaTotal,
    coalesce((
        select sum(fi.quantidade)
        from sigov.compras_empresarial_fatura_item fi
        join sigov.compras_empresarial_fatura f on f.id = fi.fatura_id and f.tenant_id = fi.tenant_id
        where fi.tenant_id = pi.tenant_id
          and fi.pedido_item_id = pi.id
          and f.status in ('APROVADA', 'EM_CONFERENCIA', 'CONFORME', 'COM_DIVERGENCIA')
    ), 0) as QuantidadeFaturadaOutras
from sigov.compras_empresarial_pedido_item pi
where pi.pedido_id = @pedidoId and pi.tenant_id = @t;";

        var itensDb = (await cn.QueryAsync<ConferenciaItemDbDto>(new CommandDefinition(
            sqlItensPedido,
            new { pedidoId = r.PedidoId, t = x.TenantId },
            tx,
            cancellationToken: ct))).ToDictionary(i => i.PedidoItemId);

        var hasDivergenciaQtd = false;
        var hasDivergenciaValor = false;
        var faturaId = Guid.NewGuid();
        var faturaItensInsert = new List<object>(r.Itens.Count);
        decimal valorItens = 0m;

        foreach (var itemReq in r.Itens)
        {
            if (!itensDb.TryGetValue(itemReq.PedidoItemId, out var piDb))
                throw new InvalidOperationException($"O item de pedido ID {itemReq.PedidoItemId} não pertence ao pedido {r.PedidoId}.");

            var saldoElegivel = Math.Max(0m, Math.Min(piDb.QuantidadePedida, piDb.QuantidadeAceitaTotal) - piDb.QuantidadeFaturadaOutras);

            if (itemReq.Quantidade > saldoElegivel)
                hasDivergenciaQtd = true;

            if (itemReq.ValorUnitario != piDb.PrecoUnitarioPedido)
                hasDivergenciaValor = true;

            var itemTotal = Math.Round(itemReq.Quantidade * itemReq.ValorUnitario, 2, MidpointRounding.AwayFromZero);
            valorItens += itemTotal;

            faturaItensInsert.Add(new
            {
                tenant_id = x.TenantId,
                fatura_id = faturaId,
                pedido_item_id = itemReq.PedidoItemId,
                quantidade = itemReq.Quantidade,
                valor_unitario = itemReq.ValorUnitario,
                valor_total = itemTotal
            });
        }

        string resultadoMatch;
        if (hasDivergenciaQtd && hasDivergenciaValor)
            resultadoMatch = "DIVERGENCIA_MISTA";
        else if (hasDivergenciaQtd)
            resultadoMatch = "DIVERGENCIA_QUANTIDADE";
        else if (hasDivergenciaValor)
            resultadoMatch = "DIVERGENCIA_VALOR";
        else
            resultadoMatch = "MATCH_TOTAL";

        var status = resultadoMatch == "MATCH_TOTAL" ? "CONFORME" : "COM_DIVERGENCIA";

        var valorLiquido = valorItens - r.ValorDesconto + r.ValorFrete + r.ValorSeguro + r.ValorOutrasDespesas;
        if (valorLiquido < 0m)
            throw new ArgumentException("O valor líquido da fatura não pode ser negativo após descontos e despesas.");

        // 6. Inserir fatura
        const string sqlInsertFatura = @"
insert into sigov.compras_empresarial_fatura (
    id, tenant_id, pedido_id, fornecedor_id, numero, serie,
    total, resultado_match, status,
    tipo_documento, chave_acesso, data_emissao, data_vencimento,
    valor_itens, valor_desconto, valor_frete, valor_seguro, valor_outras_despesas, valor_liquido,
    observacoes, created_by, updated_by, correlation_id, version
) values (
    @id, @tenant_id, @pedido_id, @fornecedor_id, @numero, @serie,
    @total, @resultado_match, @status,
    @tipo_documento, @chave_acesso, @data_emissao, @data_vencimento,
    @valor_itens, @valor_desconto, @valor_frete, @valor_seguro, @valor_outras_despesas, @valor_liquido,
    @observacoes, @created_by, @created_by, @correlation_id, 1
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlInsertFatura, new
        {
            id = faturaId,
            tenant_id = x.TenantId,
            pedido_id = r.PedidoId,
            fornecedor_id = r.FornecedorId,
            numero = r.Numero,
            serie = r.Serie,
            total = valorItens,
            resultado_match = resultadoMatch,
            status,
            tipo_documento = r.TipoDocumento,
            chave_acesso = r.ChaveAcesso,
            data_emissao = r.DataEmissao,
            data_vencimento = r.DataVencimento,
            valor_itens = valorItens,
            valor_desconto = r.ValorDesconto,
            valor_frete = r.ValorFrete,
            valor_seguro = r.ValorSeguro,
            valor_outras_despesas = r.ValorOutrasDespesas,
            valor_liquido = valorLiquido,
            observacoes = r.Observacoes,
            created_by = x.UsuarioId.ToString(),
            correlation_id = x.CorrelationId
        }, tx, cancellationToken: ct));

        // 7. Inserir itens
        const string sqlInsertItem = @"
insert into sigov.compras_empresarial_fatura_item (
    tenant_id, fatura_id, pedido_item_id, quantidade, valor_unitario, valor_total
) values (
    @tenant_id, @fatura_id, @pedido_item_id, @quantidade, @valor_unitario, @valor_total
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlInsertItem, faturaItensInsert, tx, cancellationToken: ct));

        // 8. Inserir evento
        var eventoDetalhes = JsonSerializer.Serialize(new
        {
            resultado_match = resultadoMatch,
            status,
            total = valorItens,
            valor_liquido = valorLiquido,
            itens_count = r.Itens.Count
        }, JsonOpts);

        const string sqlInsertEvento = @"
insert into sigov.compras_empresarial_fatura_evento (
    tenant_id, fatura_id, tipo, detalhes, usuario_id, correlation_id
) values (
    @tenant_id, @fatura_id, 'FATURA_CRIADA', @detalhes::jsonb, @usuario_id, @correlation_id
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlInsertEvento, new
        {
            tenant_id = x.TenantId,
            fatura_id = faturaId,
            detalhes = eventoDetalhes,
            usuario_id = x.UsuarioId,
            correlation_id = x.CorrelationId
        }, tx, cancellationToken: ct));

        // 9. Registrar idempotência
        var resultado = new FaturaComandoResultado(faturaId, status, resultadoMatch, 1, false);
        var resultadoJson = JsonSerializer.Serialize(resultado, JsonOpts);

        const string sqlIdempotencia = @"
insert into sigov.compras_empresarial_fatura_idempotencia (
    tenant_id, operacao, chave, fatura_id, request_hash, resultado
) values (
    @tenant_id, 'CRIAR_FATURA', @chave, @fatura_id, @request_hash, @resultado::jsonb
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlIdempotencia, new
        {
            tenant_id = x.TenantId,
            chave = key,
            fatura_id = faturaId,
            request_hash = requestHash,
            resultado = resultadoJson
        }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return resultado;
    }

    public async Task<FaturaComandoResultado> DecidirAsync(ComprasContext x, Guid id, DecidirFaturaRequest r, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);

        var chave = r.IdempotencyKey ?? $"DECISAO_{id}_{r.Version}_{r.Decisao}";
        var requestHash = Sha256(JsonSerializer.Serialize(r, JsonOpts));

        // 1. Idempotência
        var idempotencia = await cn.QuerySingleOrDefaultAsync<IdempotenciaDto>(new CommandDefinition(
            @"select fatura_id as FaturaId, resultado as ResultadoJson, request_hash as RequestHash
              from sigov.compras_empresarial_fatura_idempotencia
              where tenant_id = @t and operacao = @op and chave = @k",
            new { t = x.TenantId, op = $"DECIDIR_FATURA_{id}", k = chave },
            tx,
            cancellationToken: ct));

        if (idempotencia is not null)
        {
            if (idempotencia.RequestHash != requestHash)
                throw new InvalidOperationException("A chave de idempotência já foi utilizada com uma decisão diferente.");

            if (!string.IsNullOrWhiteSpace(idempotencia.ResultadoJson))
            {
                var prevRes = JsonSerializer.Deserialize<FaturaComandoResultado>(idempotencia.ResultadoJson, JsonOpts);
                if (prevRes is not null) return prevRes with { Repetido = true };
            }
        }

        // 2. Lock na fatura
        await LockFaturaAsync(cn, tx, x.TenantId, id, ct);

        // 3. Obter dados da fatura e checar concorrência
        var fatura = await cn.QuerySingleOrDefaultAsync<FaturaDecisaoDto>(new CommandDefinition(
            @"select id, pedido_id as PedidoId, status, version, resultado_match as ResultadoMatch
              from sigov.compras_empresarial_fatura
              where id = @id and tenant_id = @t",
            new { id, t = x.TenantId },
            tx,
            cancellationToken: ct));

        if (fatura is null)
            throw new InvalidOperationException("Fatura não encontrada.");

        if (fatura.Version != r.Version)
            throw new ComprasConcurrencyException("A fatura foi modificada por outro processo. Atualize a visualização antes de prosseguir.");

        if (fatura.Status is "APROVADA" or "REJEITADA" or "CANCELADA")
            throw new InvalidOperationException($"A fatura já se encontra na situação '{fatura.Status}' e não permite novas decisões.");

        string novoStatus;
        string novoMatch = fatura.ResultadoMatch;
        string tipoEvento;

        if (r.Decisao == "ACEITAR")
        {
            // Validação de saldo concorrente antes de aprovar
            await LockPedidoAsync(cn, tx, x.TenantId, fatura.PedidoId, ct);

            const string sqlValidaSaldos = @"
select
    fi.pedido_item_id as PedidoItemId,
    fi.quantidade as QuantidadeFaturada,
    pi.quantidade as QuantidadePedida,
    coalesce((
        select sum(ri.quantidade_aceita)
        from sigov.compras_empresarial_recebimento_item ri
        join sigov.compras_empresarial_recebimento r on r.id = ri.recebimento_id and r.tenant_id = ri.tenant_id
        where ri.tenant_id = fi.tenant_id
          and ri.pedido_item_id = fi.pedido_item_id
          and r.status <> 'CANCELADO'
    ), 0) as QuantidadeAceitaTotal,
    coalesce((
        select sum(ofi.quantidade)
        from sigov.compras_empresarial_fatura_item ofi
        join sigov.compras_empresarial_fatura of on of.id = ofi.fatura_id and of.tenant_id = ofi.tenant_id
        where ofi.tenant_id = fi.tenant_id
          and ofi.pedido_item_id = fi.pedido_item_id
          and of.id <> fi.fatura_id
          and of.status in ('APROVADA', 'EM_CONFERENCIA', 'CONFORME', 'COM_DIVERGENCIA')
    ), 0) as QuantidadeFaturadaOutras
from sigov.compras_empresarial_fatura_item fi
join sigov.compras_empresarial_pedido_item pi on pi.id = fi.pedido_item_id and pi.tenant_id = fi.tenant_id
where fi.fatura_id = @faturaId and fi.tenant_id = @t;";

            var itensConferencia = (await cn.QueryAsync<ConferenciaValidacaoDto>(new CommandDefinition(
                sqlValidaSaldos,
                new { faturaId = id, t = x.TenantId },
                tx,
                cancellationToken: ct))).AsList();

            foreach (var it in itensConferencia)
            {
                var saldoElegivel = Math.Max(0m, Math.Min(it.QuantidadePedida, it.QuantidadeAceitaTotal) - it.QuantidadeFaturadaOutras);
                if (it.QuantidadeFaturada > saldoElegivel)
                {
                    throw new InvalidOperationException(
                        $"Não é possível aprovar a fatura: o item {it.PedidoItemId} possui quantidade faturada ({it.QuantidadeFaturada:0.####}) superior ao saldo elegível disponível ({saldoElegivel:0.####}) decorrente de recebimentos aceitos.");
                }
            }

            novoStatus = "APROVADA";
            novoMatch = "MATCH_TOTAL";
            tipoEvento = "FATURA_APROVADA";

            const string sqlAceitar = @"
update sigov.compras_empresarial_fatura set
    status = @novoStatus,
    resultado_match = @novoMatch,
    decidido_por = @decididoPor,
    decidido_em = now(),
    justificativa = coalesce(@justificativa, justificativa),
    version = version + 1,
    updated_at = now(),
    updated_by = @decididoPor
where id = @id and tenant_id = @t and version = @version;";

            await cn.ExecuteAsync(new CommandDefinition(sqlAceitar, new
            {
                id,
                t = x.TenantId,
                version = r.Version,
                novoStatus,
                novoMatch,
                decididoPor = x.UsuarioId.ToString(),
                justificativa = r.Justificativa
            }, tx, cancellationToken: ct));
        }
        else if (r.Decisao == "REJEITAR")
        {
            novoStatus = "REJEITADA";
            tipoEvento = "FATURA_REJEITADA";

            const string sqlRejeitar = @"
update sigov.compras_empresarial_fatura set
    status = @novoStatus,
    decidido_por = @decididoPor,
    decidido_em = now(),
    motivo_rejeicao = @motivo,
    version = version + 1,
    updated_at = now(),
    updated_by = @decididoPor
where id = @id and tenant_id = @t and version = @version;";

            await cn.ExecuteAsync(new CommandDefinition(sqlRejeitar, new
            {
                id,
                t = x.TenantId,
                version = r.Version,
                novoStatus,
                decididoPor = x.UsuarioId.ToString(),
                motivo = r.Justificativa
            }, tx, cancellationToken: ct));
        }
        else
        {
            novoStatus = "CANCELADA";
            tipoEvento = "FATURA_CANCELADA";

            const string sqlCancelar = @"
update sigov.compras_empresarial_fatura set
    status = @novoStatus,
    decidido_por = @decididoPor,
    decidido_em = now(),
    justificativa = @motivo,
    version = version + 1,
    updated_at = now(),
    updated_by = @decididoPor
where id = @id and tenant_id = @t and version = @version;";

            await cn.ExecuteAsync(new CommandDefinition(sqlCancelar, new
            {
                id,
                t = x.TenantId,
                version = r.Version,
                novoStatus,
                decididoPor = x.UsuarioId.ToString(),
                motivo = r.Justificativa
            }, tx, cancellationToken: ct));
        }

        // 4. Inserir evento
        var eventoDetalhes = JsonSerializer.Serialize(new
        {
            decisao = r.Decisao,
            status = novoStatus,
            justificativa = r.Justificativa,
            versao_anterior = r.Version
        }, JsonOpts);

        const string sqlEvento = @"
insert into sigov.compras_empresarial_fatura_evento (
    tenant_id, fatura_id, tipo, detalhes, usuario_id, correlation_id
) values (
    @tenant_id, @fatura_id, @tipo, @detalhes::jsonb, @usuario_id, @correlation_id
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlEvento, new
        {
            tenant_id = x.TenantId,
            fatura_id = id,
            tipo = tipoEvento,
            detalhes = eventoDetalhes,
            usuario_id = x.UsuarioId,
            correlation_id = x.CorrelationId
        }, tx, cancellationToken: ct));

        // 5. Gravar idempotência
        var novaVersao = r.Version + 1;
        var resultado = new FaturaComandoResultado(id, novoStatus, novoMatch, novaVersao, false);
        var resultadoJson = JsonSerializer.Serialize(resultado, JsonOpts);

        const string sqlIdempotencia = @"
insert into sigov.compras_empresarial_fatura_idempotencia (
    tenant_id, operacao, chave, fatura_id, request_hash, resultado
) values (
    @tenant_id, @operacao, @chave, @fatura_id, @request_hash, @resultado::jsonb
);";

        await cn.ExecuteAsync(new CommandDefinition(sqlIdempotencia, new
        {
            tenant_id = x.TenantId,
            operacao = $"DECIDIR_FATURA_{id}",
            chave,
            fatura_id = id,
            request_hash = requestHash,
            resultado = resultadoJson
        }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return resultado;
    }

    private sealed class FaturaHeaderDto
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string Numero { get; init; } = "";
        public string Serie { get; init; } = "";
        public string TipoDocumento { get; init; } = "";
        public string? ChaveAcesso { get; init; }
        public Guid FornecedorId { get; init; }
        public string FornecedorNome { get; init; } = "";
        public string FornecedorCnpj { get; init; } = "";
        public Guid PedidoId { get; init; }
        public string PedidoNumero { get; init; } = "";
        public decimal Total { get; init; }
        public decimal ValorItens { get; init; }
        public decimal ValorDesconto { get; init; }
        public decimal ValorFrete { get; init; }
        public decimal ValorSeguro { get; init; }
        public decimal ValorOutrasDespesas { get; init; }
        public decimal ValorLiquido { get; init; }
        public string Status { get; init; } = "";
        public string ResultadoMatch { get; init; } = "";
        public DateTime? DataEmissao { get; init; }
        public DateTime? DataVencimento { get; init; }
        public string? Observacoes { get; init; }
        public string? Justificativa { get; init; }
        public string? DecididoPor { get; init; }
        public DateTimeOffset? DecididoEm { get; init; }
        public string? MotivoRejeicao { get; init; }
        public DateTimeOffset CriadaEm { get; init; }
        public long Version { get; init; }
    }

    private sealed class FaturaItemRawDto
    {
        public long Id { get; init; }
        public long PedidoItemId { get; init; }
        public string ProdutoNome { get; init; } = "";
        public string Unidade { get; init; } = "";
        public decimal QuantidadePedida { get; init; }
        public decimal PrecoUnitarioPedido { get; init; }
        public decimal QuantidadeFaturada { get; init; }
        public decimal PrecoUnitarioFatura { get; init; }
        public decimal TotalItem { get; init; }
        public decimal QuantidadeAceitaTotal { get; init; }
        public decimal QuantidadeFaturadaOutras { get; init; }
    }

    private sealed class FaturaEventoRawDto
    {
        public long Id { get; init; }
        public string Tipo { get; init; } = "";
        public string? Detalhes { get; init; }
        public Guid UsuarioId { get; init; }
        public string? Autor { get; init; }
        public DateTimeOffset OcorridoEm { get; init; }
        public string? CorrelationId { get; init; }
    }

    private sealed class PreviaPedidoDto
    {
        public Guid PedidoId { get; init; }
        public string PedidoNumero { get; init; } = "";
        public Guid FornecedorId { get; init; }
        public string FornecedorNome { get; init; } = "";
        public string FornecedorCnpj { get; init; } = "";
        public decimal TotalPedido { get; init; }
    }

    private sealed class PreviaItemRawDto
    {
        public long PedidoItemId { get; init; }
        public string ProdutoNome { get; init; } = "";
        public string Unidade { get; init; } = "";
        public decimal QuantidadePedida { get; init; }
        public decimal ValorUnitarioPedido { get; init; }
        public decimal QuantidadeAceitaTotal { get; init; }
        public decimal QuantidadeFaturadaOutras { get; init; }
    }

    private sealed class ConferenciaItemDbDto
    {
        public long PedidoItemId { get; init; }
        public decimal QuantidadePedida { get; init; }
        public decimal PrecoUnitarioPedido { get; init; }
        public decimal QuantidadeAceitaTotal { get; init; }
        public decimal QuantidadeFaturadaOutras { get; init; }
    }

    private sealed class ConferenciaValidacaoDto
    {
        public long PedidoItemId { get; init; }
        public decimal QuantidadeFaturada { get; init; }
        public decimal QuantidadePedida { get; init; }
        public decimal QuantidadeAceitaTotal { get; init; }
        public decimal QuantidadeFaturadaOutras { get; init; }
    }

    private sealed class IdempotenciaDto
    {
        public Guid FaturaId { get; init; }
        public string? ResultadoJson { get; init; }
        public string RequestHash { get; init; } = "";
    }

    private sealed class FaturaDecisaoDto
    {
        public Guid Id { get; init; }
        public Guid PedidoId { get; init; }
        public string Status { get; init; } = "";
        public long Version { get; init; }
        public string ResultadoMatch { get; init; } = "";
    }
}
