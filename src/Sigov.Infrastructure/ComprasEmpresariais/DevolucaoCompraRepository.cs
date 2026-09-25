using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.ComprasEmpresariais;

public sealed class DevolucaoCompraRepository(NpgsqlConnectionFactory factory) : IDevolucaoCompraRepository
{
    public async Task<PagedResult<DevolucaoResumo>> ListarAsync(Guid tenant, DevolucaoFiltro f, CancellationToken ct)
    {
        const string where = @"d.tenant_id=@tenant and (@fornecedor is null or coalesce(fo.nome_fantasia,fo.razao_social) ilike @fornecedor) and (@documento is null or r.documento ilike @documento) and (@situacao is null or d.situacao=@situacao) and (@responsavel is null or d.responsavel_id=@responsavel) and (@ai is null or d.created_at>=@ai) and (@af is null or d.created_at<@af::date+1) and (@xi is null or d.expedida_em>=@xi) and (@xf is null or d.expedida_em<@xf::date+1) and (@ei is null or d.entregue_em>=@ei) and (@ef is null or d.entregue_em<@ef::date+1)";
        var sql = $@"select count(*) from sigov.compras_empresarial_devolucao d join sigov.compras_empresarial_recebimento r on (r.tenant_id,r.id)=(d.tenant_id,d.recebimento_id) join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id) join sigov.compras_empresarial_fornecedor fo on (fo.tenant_id,fo.id)=(p.tenant_id,p.fornecedor_id) where {where}; select d.id,d.recebimento_id RecebimentoId,r.documento DocumentoRecebimento,coalesce(fo.nome_fantasia,fo.razao_social) Fornecedor,d.situacao,d.responsavel_id ResponsavelId,coalesce(t.nome,d.responsavel_id::text) ResponsavelNome,d.origem_fisica OrigemFisica,d.destino,d.motivo,d.created_at CriadaEm,d.expedida_em ExpedidaEm,d.entregue_em EntregueEm,d.version,count(i.id)::int Itens from sigov.compras_empresarial_devolucao d join sigov.compras_empresarial_recebimento r on (r.tenant_id,r.id)=(d.tenant_id,d.recebimento_id) join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id) join sigov.compras_empresarial_fornecedor fo on (fo.tenant_id,fo.id)=(p.tenant_id,p.fornecedor_id) left join sigov.os_tecnico t on (t.tenant_id,t.usuario_id)=(d.tenant_id,d.responsavel_id) and not t.is_deleted left join sigov.compras_empresarial_devolucao_item i on (i.tenant_id,i.devolucao_id)=(d.tenant_id,d.id) where {where} group by d.id,r.documento,fo.nome_fantasia,fo.razao_social,t.nome order by d.created_at desc,d.id desc offset @offset limit @size";
        var size = Math.Clamp(f.Tamanho, 1, 100);
        var page = Math.Max(1, f.Pagina);
        var a = new
        {
            tenant,
            fornecedor = Like(f.Fornecedor),
            documento = Like(f.DocumentoRecebimento),
            situacao = Clean(f.Situacao)?.ToUpperInvariant(),
            responsavel = f.ResponsavelId,
            ai = f.AberturaInicial,
            af = f.AberturaFinal,
            xi = f.ExpedicaoInicial,
            xf = f.ExpedicaoFinal,
            ei = f.EntregaInicial,
            ef = f.EntregaFinal,
            offset = (page - 1) * size,
            size
        };
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(sql, a, cancellationToken: ct));
        var total = await m.ReadSingleAsync<long>();
        return new((await m.ReadAsync<DevolucaoResumo>()).AsList(), page, size, total);
    }

    public async Task<DevolucaoDetalhe?> ObterAsync(Guid tenant, long id, CancellationToken ct)
    {
        const string sql = @"select d.id,d.recebimento_id RecebimentoId,r.documento DocumentoRecebimento,p.numero PedidoNumero,p.fornecedor_id FornecedorId,coalesce(fo.nome_fantasia,fo.razao_social) Fornecedor,d.situacao,d.responsavel_id ResponsavelId,coalesce(t.nome,d.responsavel_id::text) ResponsavelNome,d.origem_fisica OrigemFisica,d.destino,d.motivo,d.esfera_governo EsferaGoverno,d.tipo_entidade TipoEntidade,d.unidade_gestora UnidadeGestora,d.unidade_executora UnidadeExecutora,d.abrangencia_territorial AbrangenciaTerritorial,d.version,d.created_at CriadaEm,d.expedida_em ExpedidaEm,d.entregue_em EntregueEm,d.cancelada_em CanceladaEm,d.modalidade,d.referencia_transporte ReferenciaTransporte,d.recebedor_referencia RecebedorOuReferencia,d.documento_protocolo DocumentoProtocolo from sigov.compras_empresarial_devolucao d join sigov.compras_empresarial_recebimento r on (r.tenant_id,r.id)=(d.tenant_id,d.recebimento_id) join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id) join sigov.compras_empresarial_fornecedor fo on (fo.tenant_id,fo.id)=(p.tenant_id,p.fornecedor_id) left join sigov.os_tecnico t on (t.tenant_id,t.usuario_id)=(d.tenant_id,d.responsavel_id) and not t.is_deleted where d.tenant_id=@tenant and d.id=@id;
select i.id,i.recebimento_item_id RecebimentoItemId,pr.nome Produto,pr.unidade,ri.quantidade_rejeitada QuantidadeRejeitada,case when d.situacao='RASCUNHO' then i.quantidade else 0 end QuantidadeReservada,case when d.situacao='EXPEDIDA' then i.quantidade else 0 end QuantidadeExpedida,case when d.situacao='ENTREGUE' then i.quantidade else 0 end QuantidadeEntregue,(ri.quantidade_rejeitada-coalesce((select sum(ix.quantidade) from sigov.compras_empresarial_devolucao_item ix join sigov.compras_empresarial_devolucao dx on (dx.tenant_id,dx.id)=(ix.tenant_id,ix.devolucao_id) where ix.tenant_id=i.tenant_id and ix.recebimento_item_id=i.recebimento_item_id and dx.situacao<>'CANCELADA'),0)) SaldoElegivel from sigov.compras_empresarial_devolucao_item i join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(i.tenant_id,i.devolucao_id) join sigov.compras_empresarial_recebimento_item ri on (ri.tenant_id,ri.id)=(i.tenant_id,i.recebimento_item_id) join sigov.estoque_produto pr on (pr.tenant_id,pr.id)=(ri.tenant_id,ri.produto_id) where i.tenant_id=@tenant and i.devolucao_id=@id order by i.id;
select e.id,e.tipo,e.estado_anterior EstadoAnterior,e.estado_novo EstadoNovo,e.detalhes::text Detalhes,e.usuario_id UsuarioId,e.ocorrido_em OcorridoEm,e.correlation_id CorrelationId,coalesce(ut.nome,e.usuario_id::text) AutorNome from sigov.compras_empresarial_devolucao_evento e left join sigov.os_tecnico ut on (ut.tenant_id,ut.usuario_id)=(e.tenant_id,e.usuario_id) and not ut.is_deleted where e.tenant_id=@tenant and e.devolucao_id=@id order by e.ocorrido_em,e.id";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { tenant, id }, cancellationToken: ct));
        var h = await m.ReadSingleOrDefaultAsync<Head>();
        if (h is null) return null;
        return new(h.Id, h.RecebimentoId, h.DocumentoRecebimento, h.PedidoNumero, h.FornecedorId, h.Fornecedor, h.Situacao, h.ResponsavelId, h.ResponsavelNome, h.OrigemFisica, h.Destino, h.Motivo, h.EsferaGoverno, h.TipoEntidade, h.UnidadeGestora, h.UnidadeExecutora, h.AbrangenciaTerritorial, h.Version, h.CriadaEm, h.ExpedidaEm, h.EntregueEm, h.CanceladaEm, h.Modalidade, h.ReferenciaTransporte, h.RecebedorOuReferencia, h.DocumentoProtocolo, (await m.ReadAsync<DevolucaoItemDetalhe>()).AsList(), (await m.ReadAsync<DevolucaoEvento>()).AsList());
    }

    public async Task<OrigemDevolucao?> ObterOrigemAsync(Guid tenant, Guid recebimentoId, CancellationToken ct)
    {
        const string sql = @"select r.id RecebimentoId,r.documento,coalesce(f.nome_fantasia,f.razao_social) Fornecedor
from sigov.compras_empresarial_recebimento r
join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id)
join sigov.compras_empresarial_fornecedor f on (f.tenant_id,f.id)=(p.tenant_id,p.fornecedor_id)
where r.tenant_id=@tenant and r.id=@recebimentoId
  and r.status in('COM_DIVERGENCIA','CONCLUIDO')
  and r.resultado_inspecao in('RECUSADO','REPROVADO','REPROVADO_PARCIAL','ACEITO_COM_RESSALVA')
  and not exists(select 1 from sigov.compras_empresarial_recebimento_item rx where rx.tenant_id=r.tenant_id and rx.recebimento_id=r.id and rx.quantidade_conferencia>0);
select 0::bigint id,ri.id RecebimentoItemId,pr.nome Produto,pr.unidade,ri.quantidade_rejeitada QuantidadeRejeitada,
       coalesce(sum(di.quantidade) filter(where d.situacao='RASCUNHO'),0) QuantidadeReservada,
       coalesce(sum(di.quantidade) filter(where d.situacao='EXPEDIDA'),0) QuantidadeExpedida,
       coalesce(sum(di.quantidade) filter(where d.situacao='ENTREGUE'),0) QuantidadeEntregue,
       (ri.quantidade_rejeitada-coalesce(sum(di.quantidade) filter(where d.situacao<>'CANCELADA'),0)) SaldoElegivel
from sigov.compras_empresarial_recebimento_item ri
join sigov.estoque_produto pr on (pr.tenant_id,pr.id)=(ri.tenant_id,ri.produto_id)
left join sigov.compras_empresarial_devolucao_item di on (di.tenant_id,di.recebimento_item_id)=(ri.tenant_id,ri.id)
left join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id)
where ri.tenant_id=@tenant and ri.recebimento_id=@recebimentoId and ri.quantidade_rejeitada>0
group by ri.id,pr.nome,pr.unidade order by ri.id;";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { tenant, recebimentoId }, cancellationToken: ct));
        var h = await m.ReadSingleOrDefaultAsync<OriginHead>();
        return h is null ? null : new(h.RecebimentoId, h.Documento, h.Fornecedor, (await m.ReadAsync<DevolucaoItemDetalhe>()).AsList());
    }

    public async Task<DevolucaoParaEdicao?> ObterParaEdicaoAsync(Guid tenant, long devolucaoId, CancellationToken ct)
    {
        const string sql = @"select d.id, d.recebimento_id RecebimentoId, r.documento DocumentoRecebimento, coalesce(f.nome_fantasia,f.razao_social) Fornecedor, d.situacao, d.version, d.motivo, d.responsavel_id ResponsavelId, t.nome ResponsavelNome, d.origem_fisica OrigemFisica, d.destino Destino
from sigov.compras_empresarial_devolucao d
join sigov.compras_empresarial_recebimento r on (r.tenant_id,r.id)=(d.tenant_id,d.recebimento_id)
join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id)
join sigov.compras_empresarial_fornecedor f on (f.tenant_id,f.id)=(p.tenant_id,p.fornecedor_id)
left join sigov.os_tecnico t on (t.tenant_id,t.usuario_id)=(d.tenant_id,d.responsavel_id) and not t.is_deleted
where d.tenant_id=@tenant and d.id=@devolucaoId and d.situacao='RASCUNHO';";
        await using var cn = factory.CreateConnection();
        var h = await cn.QuerySingleOrDefaultAsync<EditHeadRow>(new CommandDefinition(sql, new { tenant, devolucaoId }, cancellationToken: ct));
        if (h is null) return null;

        const string itemsSql = @"select coalesce(my_di.id, 0)::bigint id,ri.id RecebimentoItemId,pr.nome Produto,pr.unidade,ri.quantidade_rejeitada QuantidadeRejeitada,
       coalesce(my_di.quantidade, 0) QuantidadeReservada,
       coalesce(sum(di.quantidade) filter(where d.situacao='EXPEDIDA'),0) QuantidadeExpedida,
       coalesce(sum(di.quantidade) filter(where d.situacao='ENTREGUE'),0) QuantidadeEntregue,
       (ri.quantidade_rejeitada-coalesce(sum(di.quantidade) filter(where d.situacao<>'CANCELADA' and d.id<>@devolucaoId),0)) SaldoElegivel
from sigov.compras_empresarial_recebimento_item ri
join sigov.estoque_produto pr on (pr.tenant_id,pr.id)=(ri.tenant_id,ri.produto_id)
left join sigov.compras_empresarial_devolucao_item my_di on (my_di.tenant_id,my_di.recebimento_item_id,my_di.devolucao_id)=(ri.tenant_id,ri.id,@devolucaoId)
left join sigov.compras_empresarial_devolucao_item di on (di.tenant_id,di.recebimento_item_id)=(ri.tenant_id,ri.id)
left join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id)
where ri.tenant_id=@tenant and ri.recebimento_id=@recebimentoId and ri.quantidade_rejeitada>0
group by ri.id,pr.nome,pr.unidade,my_di.id,my_di.quantidade order by ri.id;";
        var itens = (await cn.QueryAsync<DevolucaoItemDetalhe>(new CommandDefinition(itemsSql, new { tenant, recebimentoId = h.RecebimentoId, devolucaoId }, cancellationToken: ct))).AsList();
        return new(h.Id, h.RecebimentoId, h.DocumentoRecebimento, h.Fornecedor, h.Situacao, h.Version, h.Motivo, h.ResponsavelId, h.ResponsavelNome, h.OrigemFisica, h.Destino, itens);
    }

    public async Task<IReadOnlyList<OrigemElegivelResumo>> PesquisarOrigensElegiveisAsync(Guid tenant, string? busca, CancellationToken ct)
    {
        const string sql = @"select r.id RecebimentoId,r.documento Documento,p.numero PedidoNumero,coalesce(f.nome_fantasia,f.razao_social) Fornecedor,
       count(distinct ri.id)::int ItensRejeitados,
       sum(ri.quantidade_rejeitada-coalesce((select sum(di.quantidade) from sigov.compras_empresarial_devolucao_item di join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id) where di.tenant_id=ri.tenant_id and di.recebimento_item_id=ri.id and d.situacao<>'CANCELADA'),0)) SaldoTotalElegivel,
       r.created_at ConcluidoEm
from sigov.compras_empresarial_recebimento r
join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id)
join sigov.compras_empresarial_fornecedor f on (f.tenant_id,f.id)=(p.tenant_id,p.fornecedor_id)
join sigov.compras_empresarial_recebimento_item ri on (ri.tenant_id,ri.recebimento_id)=(r.tenant_id,r.id) and ri.quantidade_rejeitada>0
where r.tenant_id=@tenant
  and r.status in('COM_DIVERGENCIA','CONCLUIDO')
  and r.resultado_inspecao in('RECUSADO','REPROVADO','REPROVADO_PARCIAL','ACEITO_COM_RESSALVA')
  and not exists(select 1 from sigov.compras_empresarial_recebimento_item rx where rx.tenant_id=r.tenant_id and rx.recebimento_id=r.id and rx.quantidade_conferencia>0)
  and (@term is null or r.documento ilike @term or p.numero ilike @term or f.nome_fantasia ilike @term or f.razao_social ilike @term)
group by r.id,r.documento,p.numero,f.nome_fantasia,f.razao_social,r.created_at
having sum(ri.quantidade_rejeitada-coalesce((select sum(di.quantidade) from sigov.compras_empresarial_devolucao_item di join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id) where di.tenant_id=ri.tenant_id and di.recebimento_item_id=ri.id and d.situacao<>'CANCELADA'),0)) > 0
order by r.created_at desc limit 50";
        await using var cn = factory.CreateConnection();
        return (await cn.QueryAsync<OrigemElegivelResumo>(new CommandDefinition(sql, new { tenant, term = Like(busca) }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<ResponsavelDivergencia>> PesquisarResponsaveisAsync(Guid tenant, string? busca, int pagina, int tamanho, CancellationToken ct)
    {
        const string sql = @"select t.usuario_id UsuarioId,t.nome Nome,coalesce(t.especialidade,'Técnico Operacional') Vinculo,coalesce(t.regiao,e.nome,'Operação') Unidade
from sigov.os_tecnico t
left join sigov.os_equipe e on e.tenant_id=t.tenant_id and e.id=t.equipe_id
where t.tenant_id=@tenant and not t.is_deleted and t.status in('DISPONIVEL','EM_ATENDIMENTO')
  and (@term is null or t.nome ilike @term)
order by t.nome,t.usuario_id offset @offset limit @tamanho";
        await using var cn = factory.CreateConnection();
        return (await cn.QueryAsync<ResponsavelDivergencia>(new CommandDefinition(sql, new { tenant, term = Like(busca), offset = (pagina - 1) * tamanho, tamanho }, cancellationToken: ct))).AsList();
    }

    public async Task<ContextoInstitucionalSnapshot?> ObterContextoInstitucionalAsync(Guid tenant, CancellationToken ct)
    {
        const string sql = @"select
  coalesce(e.esfera_governo,'municipal') as EsferaGoverno,
  coalesce(e.tipo_entidade,'Administração Direta') as TipoEntidade,
  os.nome as OrgaoSuperior,
  coalesce(ug.nome,e.nome,'Unidade Gestora Central') as UnidadeGestora,
  coalesce(ue.nome,e.nome,'Unidade Executora Central') as UnidadeExecutora,
  coalesce(e.hierarquia_administrativa,e.nome,'Estrutura Central') as HierarquiaAdministrativa,
  coalesce(e.abrangencia_territorial,'Municipal') as AbrangenciaTerritorial,
  e.uf as Uf,
  coalesce(e.municipio,e.nome) as Municipio,
  coalesce(e.regiao_jurisdicao,'Sede') as Regiao,
  coalesce(e.regiao_jurisdicao,'Sede') as Jurisdicao
from sigov.enterprise_tenant_mapping m
join sigov.entidade e on e.tenant_id=m.core_tenant_id and e.ativo and not e.is_deleted
left join sigov.entidade os on os.id=e.orgao_superior_id and os.ativo and not os.is_deleted
left join sigov.unidade_organizacional ug on ug.id=e.unidade_gestora_id and ug.ativo and not ug.is_deleted
left join sigov.unidade_organizacional ue on ue.id=e.unidade_executora_id and ue.ativo and not ue.is_deleted
where m.enterprise_tenant_id=@tenant and m.ativo
order by e.id limit 1";
        await using var cn = factory.CreateConnection();
        return await cn.QuerySingleOrDefaultAsync<ContextoInstitucionalSnapshot>(new CommandDefinition(sql, new { tenant }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DestinacaoRejeitadoItem>> ObterAcompanhamentoDestinacaoAsync(Guid tenant, Guid recebimentoId, CancellationToken ct)
    {
        const string sql = @"select
  ri.id as RecebimentoItemId,
  pr.nome as Produto,
  pr.unidade as Unidade,
  ri.quantidade_rejeitada as QuantidadeRejeitada,
  coalesce(sum(di.quantidade) filter (where d.situacao='RASCUNHO'), 0) as QuantidadeReservada,
  coalesce(sum(di.quantidade) filter (where d.situacao='EXPEDIDA'), 0) as QuantidadeExpedidaNaoEntregue,
  coalesce(sum(di.quantidade) filter (where d.situacao='ENTREGUE'), 0) as QuantidadeEntregue,
  (ri.quantidade_rejeitada - coalesce(sum(di.quantidade) filter (where d.situacao<>'CANCELADA'), 0)) as SaldoSemDestinacao
from sigov.compras_empresarial_recebimento_item ri
join sigov.estoque_produto pr on (pr.tenant_id,pr.id)=(ri.tenant_id,ri.produto_id)
left join sigov.compras_empresarial_devolucao_item di on (di.tenant_id,di.recebimento_item_id)=(ri.tenant_id,ri.id)
left join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id)
where ri.tenant_id=@tenant and ri.recebimento_id=@recebimentoId and ri.quantidade_rejeitada>0
group by ri.id,pr.nome,pr.unidade order by ri.id";
        await using var cn = factory.CreateConnection();
        var rows = await cn.QueryAsync<DestinacaoRow>(new CommandDefinition(sql, new { tenant, recebimentoId }, cancellationToken: ct));
        return rows.Select(r =>
        {
            var inconsistente = r.SaldoSemDestinacao < 0;
            var diag = inconsistente
                ? $"Inconsistência detectada: a soma das destinações ({r.QuantidadeReservada + r.QuantidadeExpedidaNaoEntregue + r.QuantidadeEntregue:0.####}) excede a quantidade rejeitada ({r.QuantidadeRejeitada:0.####})."
                : null;
            return new DestinacaoRejeitadoItem(r.RecebimentoItemId, r.Produto, r.Unidade, r.QuantidadeRejeitada, r.QuantidadeReservada, r.QuantidadeExpedidaNaoEntregue, r.QuantidadeEntregue, r.SaldoSemDestinacao, inconsistente, diag);
        }).AsList();
    }

    public async Task<IReadOnlyList<DevolucaoResumo>> ListarPorRecebimentoAsync(Guid tenant, Guid recebimentoId, CancellationToken ct)
    {
        const string sql = @"select d.id,d.recebimento_id RecebimentoId,r.documento DocumentoRecebimento,coalesce(fo.nome_fantasia,fo.razao_social) Fornecedor,d.situacao,d.responsavel_id ResponsavelId,coalesce(t.nome,d.responsavel_id::text) ResponsavelNome,d.origem_fisica OrigemFisica,d.destino,d.motivo,d.created_at CriadaEm,d.expedida_em ExpedidaEm,d.entregue_em EntregueEm,d.version,count(i.id)::int Itens
from sigov.compras_empresarial_devolucao d
join sigov.compras_empresarial_recebimento r on (r.tenant_id,r.id)=(d.tenant_id,d.recebimento_id)
join sigov.compras_empresarial_pedido p on (p.tenant_id,p.id)=(r.tenant_id,r.pedido_id)
join sigov.compras_empresarial_fornecedor fo on (fo.tenant_id,fo.id)=(p.tenant_id,p.fornecedor_id)
left join sigov.os_tecnico t on (t.tenant_id,t.usuario_id)=(d.tenant_id,d.responsavel_id) and not t.is_deleted
left join sigov.compras_empresarial_devolucao_item i on (i.tenant_id,i.devolucao_id)=(d.tenant_id,d.id)
where d.tenant_id=@tenant and d.recebimento_id=@recebimentoId
group by d.id,r.documento,fo.nome_fantasia,fo.razao_social,t.nome order by d.created_at desc,d.id desc";
        await using var cn = factory.CreateConnection();
        return (await cn.QueryAsync<DevolucaoResumo>(new CommandDefinition(sql, new { tenant, recebimentoId }, cancellationToken: ct))).AsList();
    }

    public async Task<DevolucaoComandoResultado> CriarAsync(ComprasContext c, CriarDevolucaoRequest r, CancellationToken ct)
    {
        var payload = new
        {
            r.RecebimentoId,
            r.Motivo,
            r.ResponsavelId,
            r.OrigemFisica,
            r.Destino,
            Itens = r.Itens.OrderBy(x => x.RecebimentoItemId)
        };
        return await Write(c, "CRIAR", r.IdempotencyKey, payload, null, async (cn, tx) =>
        {
            await LockReceipt(cn, tx, c.TenantId, r.RecebimentoId, ct);

            // Revalida a elegibilidade estrita do recebimento dentro da transação
            const string eligibilityCheck = @"select exists(
                select 1 from sigov.compras_empresarial_recebimento r
                where r.tenant_id=@tenant and r.id=@recebimento
                  and r.status in('COM_DIVERGENCIA','CONCLUIDO')
                  and r.resultado_inspecao in('RECUSADO','REPROVADO','REPROVADO_PARCIAL','ACEITO_COM_RESSALVA')
                  and not exists(select 1 from sigov.compras_empresarial_recebimento_item rx where rx.tenant_id=r.tenant_id and rx.recebimento_id=r.id and rx.quantidade_conferencia>0)
            )";
            if (!await cn.ExecuteScalarAsync<bool>(new CommandDefinition(eligibilityCheck, new { tenant = c.TenantId, recebimento = r.RecebimentoId }, tx, cancellationToken: ct)))
                throw new InvalidOperationException("O recebimento não está elegível para devolução ou ainda possui conferência em andamento.");

            // Valida responsável ativo e elegível
            const string respCheck = @"select exists(select 1 from sigov.os_tecnico where tenant_id=@tenant and usuario_id=@responsavel and not is_deleted and status in('DISPONIVEL','EM_ATENDIMENTO'))";
            if (!await cn.ExecuteScalarAsync<bool>(new CommandDefinition(respCheck, new { tenant = c.TenantId, responsavel = r.ResponsavelId }, tx, cancellationToken: ct)))
                throw new ArgumentException("O responsável selecionado não está ativo e elegível no contexto autorizado.");

            // Resolve contexto institucional autorizado a partir da entidade vinculada ao tenant
            var ctx = await cn.QuerySingleOrDefaultAsync<ContextoInstitucionalSnapshot>(new CommandDefinition(@"select
  coalesce(e.esfera_governo,'municipal') as EsferaGoverno,
  coalesce(e.tipo_entidade,'Administração Direta') as TipoEntidade,
  os.nome as OrgaoSuperior,
  coalesce(ug.nome,e.nome,'Unidade Gestora Central') as UnidadeGestora,
  coalesce(ue.nome,e.nome,'Unidade Executora Central') as UnidadeExecutora,
  coalesce(e.hierarquia_administrativa,e.nome,'Estrutura Central') as HierarquiaAdministrativa,
  coalesce(e.abrangencia_territorial,'Municipal') as AbrangenciaTerritorial,
  e.uf as Uf,
  coalesce(e.municipio,e.nome) as Municipio,
  coalesce(e.regiao_jurisdicao,'Sede') as Regiao,
  coalesce(e.regiao_jurisdicao,'Sede') as Jurisdicao
from sigov.enterprise_tenant_mapping m
join sigov.entidade e on e.tenant_id=m.core_tenant_id and e.ativo and not e.is_deleted
left join sigov.entidade os on os.id=e.orgao_superior_id and os.ativo and not os.is_deleted
left join sigov.unidade_organizacional ug on ug.id=e.unidade_gestora_id and ug.ativo and not ug.is_deleted
left join sigov.unidade_organizacional ue on ue.id=e.unidade_executora_id and ue.ativo and not ue.is_deleted
where m.enterprise_tenant_id=@tenant and m.ativo order by e.id limit 1", new { tenant = c.TenantId }, tx, cancellationToken: ct));

            if (ctx is null)
                throw new InvalidOperationException("Configuração institucional obrigatória ausente para o contexto autorizado.");

            await LockItems(cn, tx, c.TenantId, r.RecebimentoId, r.Itens.Select(x => x.RecebimentoItemId), ct);
            await ValidateBalances(cn, tx, c.TenantId, r.RecebimentoId, r.Itens, null, ct);

            var id = await cn.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.compras_empresarial_devolucao(tenant_id,recebimento_id,responsavel_id,motivo,origem_fisica,destino,esfera_governo,tipo_entidade,orgao_superior,unidade_gestora,unidade_executora,hierarquia_administrativa,abrangencia_territorial,uf,municipio,regiao,jurisdicao,created_by,updated_by,correlation_id) values(@tenant,@RecebimentoId,@ResponsavelId,@Motivo,@OrigemFisica,@Destino,@EsferaGoverno,@TipoEntidade,@OrgaoSuperior,@UnidadeGestora,@UnidadeExecutora,@HierarquiaAdministrativa,@AbrangenciaTerritorial,@Uf,@Municipio,@Regiao,@Jurisdicao,@user,@user,@corr) returning id", new
            {
                tenant = c.TenantId,
                r.RecebimentoId,
                r.ResponsavelId,
                r.Motivo,
                r.OrigemFisica,
                r.Destino,
                ctx.EsferaGoverno,
                ctx.TipoEntidade,
                ctx.OrgaoSuperior,
                ctx.UnidadeGestora,
                ctx.UnidadeExecutora,
                ctx.HierarquiaAdministrativa,
                ctx.AbrangenciaTerritorial,
                ctx.Uf,
                ctx.Municipio,
                ctx.Regiao,
                ctx.Jurisdicao,
                user = c.UsuarioId,
                corr = c.CorrelationId
            }, tx, cancellationToken: ct));

            foreach (var i in r.Itens)
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_devolucao_item(tenant_id,devolucao_id,recebimento_item_id,quantidade) values(@tenant,@id,@RecebimentoItemId,@Quantidade)", new { tenant = c.TenantId, id, i.RecebimentoItemId, i.Quantidade }, tx, cancellationToken: ct));

            await Event(cn, tx, c, id, "CRIADA", null, "RASCUNHO", new { r.Motivo, itens = r.Itens, responsavel = r.ResponsavelId }, ct);
            return new Result(id, "RASCUNHO", 1);
        }, ct);
    }

    public Task<DevolucaoComandoResultado> EditarAsync(ComprasContext c, long id, EditarDevolucaoRequest r, CancellationToken ct) =>
        Mutate(c, id, "EDITAR", r.IdempotencyKey, r, ct, async (cn, tx, h) =>
        {
            Require(h, r.Version, "RASCUNHO");

            const string respCheck = @"select exists(select 1 from sigov.os_tecnico where tenant_id=@tenant and usuario_id=@responsavel and not is_deleted and status in('DISPONIVEL','EM_ATENDIMENTO'))";
            if (!await cn.ExecuteScalarAsync<bool>(new CommandDefinition(respCheck, new { tenant = c.TenantId, responsavel = r.ResponsavelId }, tx, cancellationToken: ct)))
                throw new ArgumentException("O responsável selecionado não está ativo e elegível no contexto autorizado.");

            var itensAntes = (await cn.QueryAsync<DevolucaoItemInput>(new CommandDefinition("select recebimento_item_id RecebimentoItemId,quantidade Quantidade from sigov.compras_empresarial_devolucao_item where tenant_id=@tenant and devolucao_id=@id", new { tenant = c.TenantId, id }, tx, cancellationToken: ct))).AsList();

            await LockItems(cn, tx, c.TenantId, h.RecebimentoId, r.Itens.Select(x => x.RecebimentoItemId), ct);
            await ValidateBalances(cn, tx, c.TenantId, h.RecebimentoId, r.Itens, id, ct);

            await cn.ExecuteAsync(new CommandDefinition("delete from sigov.compras_empresarial_devolucao_item where tenant_id=@tenant and devolucao_id=@id; update sigov.compras_empresarial_devolucao set motivo=@Motivo,responsavel_id=@ResponsavelId,origem_fisica=@OrigemFisica,destino=@Destino,version=version+1,updated_at=now(),updated_by=@user,correlation_id=@corr where tenant_id=@tenant and id=@id", new { tenant = c.TenantId, id, r.Motivo, r.ResponsavelId, r.OrigemFisica, r.Destino, user = c.UsuarioId, corr = c.CorrelationId }, tx, cancellationToken: ct));

            foreach (var i in r.Itens)
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_devolucao_item(tenant_id,devolucao_id,recebimento_item_id,quantidade) values(@tenant,@id,@RecebimentoItemId,@Quantidade)", new { tenant = c.TenantId, id, i.RecebimentoItemId, i.Quantidade }, tx, cancellationToken: ct));

            await Event(cn, tx, c, id, "RASCUNHO_EDITADO", "RASCUNHO", "RASCUNHO", new
            {
                antes = new { motivo = h.Motivo, responsavel = h.ResponsavelId, itens = itensAntes },
                depois = new { motivo = r.Motivo, responsavel = r.ResponsavelId, itens = r.Itens }
            }, ct);
            return "RASCUNHO";
        });

    public Task<DevolucaoComandoResultado> ExpedirAsync(ComprasContext c, long id, ExpedirDevolucaoRequest r, CancellationToken ct) =>
        Mutate(c, id, "EXPEDIR", r.IdempotencyKey, r, ct, async (cn, tx, h) =>
        {
            Require(h, r.Version, "RASCUNHO");
            if (r.SaidaEm > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new ArgumentException("A confirmação de saída física não pode registrar data futura.");

            var items = (await cn.QueryAsync<DevolucaoItemInput>(new CommandDefinition("select recebimento_item_id RecebimentoItemId,quantidade Quantidade from sigov.compras_empresarial_devolucao_item where tenant_id=@tenant and devolucao_id=@id", new { tenant = c.TenantId, id }, tx, cancellationToken: ct))).AsList();
            if (items.Count == 0)
                throw new InvalidOperationException("A devolução não possui itens reservados.");

            await LockItems(cn, tx, c.TenantId, h.RecebimentoId, items.Select(x => x.RecebimentoItemId), ct);
            await ValidateBalances(cn, tx, c.TenantId, h.RecebimentoId, items, id, ct);

            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_devolucao set situacao='EXPEDIDA',expedida_em=@SaidaEm,modalidade=@Modalidade,transportadora=@Transportadora,referencia_transporte=@ReferenciaTransporte,version=version+1,updated_at=now(),updated_by=@user,correlation_id=@corr where tenant_id=@tenant and id=@id", new { tenant = c.TenantId, id, r.SaidaEm, r.Modalidade, r.Transportadora, r.ReferenciaTransporte, user = c.UsuarioId, corr = c.CorrelationId }, tx, cancellationToken: ct));
            await Event(cn, tx, c, id, "EXPEDIDA", "RASCUNHO", "EXPEDIDA", new { r.SaidaEm, r.Modalidade, r.Transportadora, r.ReferenciaTransporte }, ct);
            return "EXPEDIDA";
        });

    public Task<DevolucaoComandoResultado> EntregarAsync(ComprasContext c, long id, EntregarDevolucaoRequest r, CancellationToken ct) =>
        Mutate(c, id, "ENTREGAR", r.IdempotencyKey, r, ct, async (cn, tx, h) =>
        {
            Require(h, r.Version, "EXPEDIDA");
            if (r.EntregueEm > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new ArgumentException("A confirmação de entrega física não pode registrar data futura.");
            if (!h.ExpedidaEm.HasValue || r.EntregueEm < h.ExpedidaEm.Value)
                throw new ArgumentException("A entrega não pode ser anterior à expedição.");
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_devolucao set situacao='ENTREGUE',entregue_em=@EntregueEm,recebedor_referencia=@RecebedorOuReferencia,documento_protocolo=@DocumentoProtocolo,observacao_entrega=@Observacao,version=version+1,updated_at=now(),updated_by=@user,correlation_id=@corr where tenant_id=@tenant and id=@id", new { tenant = c.TenantId, id, r.EntregueEm, r.RecebedorOuReferencia, r.DocumentoProtocolo, r.Observacao, user = c.UsuarioId, corr = c.CorrelationId }, tx, cancellationToken: ct));
            await Event(cn, tx, c, id, "ENTREGUE", "EXPEDIDA", "ENTREGUE", new { r.EntregueEm, r.RecebedorOuReferencia, r.DocumentoProtocolo, r.Observacao }, ct);
            return "ENTREGUE";
        });

    public Task<DevolucaoComandoResultado> CancelarAsync(ComprasContext c, long id, CancelarDevolucaoRequest r, CancellationToken ct) =>
        Mutate(c, id, "CANCELAR", r.IdempotencyKey, r, ct, async (cn, tx, h) =>
        {
            Require(h, r.Version, "RASCUNHO");
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_devolucao set situacao='CANCELADA',cancelada_em=now(),justificativa_cancelamento=@Justificativa,version=version+1,updated_at=now(),updated_by=@user,correlation_id=@corr where tenant_id=@tenant and id=@id", new { tenant = c.TenantId, id, r.Justificativa, user = c.UsuarioId, corr = c.CorrelationId }, tx, cancellationToken: ct));
            await Event(cn, tx, c, id, "CANCELADA", "RASCUNHO", "CANCELADA", new { r.Justificativa }, ct);
            return "CANCELADA";
        });

    private async Task<DevolucaoComandoResultado> Mutate<T>(ComprasContext c, long id, string op, string key, T payload, CancellationToken ct, Func<NpgsqlConnection, NpgsqlTransaction, Locked, Task<string>> body) =>
        await Write(c, op, key, new { id, payload }, id, async (cn, tx) =>
        {
            var recebimentoId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition("select recebimento_id from sigov.compras_empresarial_devolucao where tenant_id=@tenant and id=@id", new { tenant = c.TenantId, id }, tx, cancellationToken: ct));
            if (!recebimentoId.HasValue) throw new KeyNotFoundException("Devolução não encontrada no contexto autorizado.");
            await LockReceipt(cn, tx, c.TenantId, recebimentoId.Value, ct);
            var h = await cn.QuerySingleAsync<Locked>(new CommandDefinition("select id,recebimento_id RecebimentoId,situacao,version,expedida_em ExpedidaEm,motivo Motivo,responsavel_id ResponsavelId from sigov.compras_empresarial_devolucao where tenant_id=@tenant and id=@id for update", new { tenant = c.TenantId, id }, tx, cancellationToken: ct));
            var state = await body(cn, tx, h);
            return new(id, state, h.Version + 1);
        }, ct);

    private async Task<DevolucaoComandoResultado> Write(ComprasContext c, string op, string key, object payload, long? expectedId, Func<NpgsqlConnection, NpgsqlTransaction, Task<Result>> action, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);
        var hash = Hash(payload);
        await cn.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(hashtextextended(@lock,0))", new { @lock = $"{c.TenantId:D}|DEVOLUCAO|{op}|{key}" }, tx, cancellationToken: ct));
        var old = await cn.QuerySingleOrDefaultAsync<Idem>(new CommandDefinition("select request_hash RequestHash,devolucao_id DevolucaoId,situacao,version from sigov.compras_empresarial_devolucao_idempotencia where tenant_id=@tenant and operacao=@op and chave=@key", new { tenant = c.TenantId, op, key }, tx, cancellationToken: ct));
        if (old is not null)
        {
            if (old.RequestHash != hash || (expectedId.HasValue && old.DevolucaoId != expectedId))
                throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
            await tx.CommitAsync(ct);
            return new(old.DevolucaoId, old.Situacao, old.Version, true);
        }
        var result = await action(cn, tx);
        await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_devolucao_idempotencia(tenant_id,operacao,chave,request_hash,devolucao_id,situacao,version) values(@tenant,@op,@key,@hash,@id,@state,@version)", new { tenant = c.TenantId, op, key, hash, id = result.Id, state = result.State, version = result.Version }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return new(result.Id, result.State, result.Version, false);
    }

    private static async Task LockReceipt(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenant, Guid recebimento, CancellationToken ct)
    {
        if (!await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select coalesce((select true from sigov.compras_empresarial_recebimento where tenant_id=@tenant and id=@recebimento for update),false)", new { tenant, recebimento }, tx, cancellationToken: ct)))
            throw new KeyNotFoundException("Recebimento não encontrado no contexto autorizado.");
    }

    private static Task<int> LockItems(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenant, Guid recebimento, IEnumerable<long> ids, CancellationToken ct) =>
        cn.ExecuteAsync(new CommandDefinition("select id from sigov.compras_empresarial_recebimento_item where tenant_id=@tenant and recebimento_id=@recebimento and id=any(@ids) order by id for update", new { tenant, recebimento, ids = ids.Distinct().Order().ToArray() }, tx, cancellationToken: ct));

    private static async Task ValidateBalances(NpgsqlConnection cn, NpgsqlTransaction tx, Guid tenant, Guid recebimento, IReadOnlyList<DevolucaoItemInput> items, long? exclude, CancellationToken ct)
    {
        foreach (var i in items.OrderBy(x => x.RecebimentoItemId))
        {
            var available = await cn.ExecuteScalarAsync<decimal?>(new CommandDefinition(@"select ri.quantidade_rejeitada-coalesce((select sum(di.quantidade) from sigov.compras_empresarial_devolucao_item di join sigov.compras_empresarial_devolucao d on (d.tenant_id,d.id)=(di.tenant_id,di.devolucao_id) where di.tenant_id=ri.tenant_id and di.recebimento_item_id=ri.id and d.situacao<>'CANCELADA' and (@exclude is null or d.id<>@exclude)),0) from sigov.compras_empresarial_recebimento_item ri where ri.tenant_id=@tenant and ri.recebimento_id=@recebimento and ri.id=@item and ri.quantidade_rejeitada>0", new { tenant, recebimento, item = i.RecebimentoItemId, exclude }, tx, cancellationToken: ct));
            if (!available.HasValue) throw new ArgumentException("Item rejeitado não pertence ao recebimento autorizado.");
            if (i.Quantidade > available.Value) throw new ComprasConcurrencyException("A quantidade excede o saldo rejeitado ainda elegível.");
        }
    }

    private static void Require(Locked h, long version, string state)
    {
        if (h.Version != version) throw new ComprasConcurrencyException("A devolução foi alterada; revise o estado atual.");
        if (h.Situacao != state) throw new InvalidOperationException($"A ação exige devolução em {state}.");
    }

    private static Task<int> Event(NpgsqlConnection cn, NpgsqlTransaction tx, ComprasContext c, long id, string type, string? before, string after, object details, CancellationToken ct) =>
        cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_devolucao_evento(tenant_id,devolucao_id,tipo,estado_anterior,estado_novo,detalhes,usuario_id,correlation_id) values(@tenant,@id,@type,@before,@after,@details::jsonb,@user,@corr)", new { tenant = c.TenantId, id, type, before, after, details = JsonSerializer.Serialize(details), user = c.UsuarioId, corr = c.CorrelationId }, tx, cancellationToken: ct));

    private static string Hash(object payload) => "v2:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))).ToLowerInvariant();
    private static string? Like(string? s) => string.IsNullOrWhiteSpace(s) ? null : $"%{s.Trim()}%";
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed record Result(long Id, string State, long Version);
    private sealed record Idem(string RequestHash, long DevolucaoId, string Situacao, long Version);
    private sealed record Locked(long Id, Guid RecebimentoId, string Situacao, long Version, DateTimeOffset? ExpedidaEm, string Motivo, Guid ResponsavelId);
    private sealed record OriginHead(Guid RecebimentoId, string Documento, string Fornecedor);
    private sealed record EditHeadRow(long Id, Guid RecebimentoId, string DocumentoRecebimento, string Fornecedor, string Situacao, long Version, string Motivo, Guid ResponsavelId, string? ResponsavelNome, string OrigemFisica, string Destino);
    private sealed record Head(long Id, Guid RecebimentoId, string DocumentoRecebimento, string PedidoNumero, Guid FornecedorId, string Fornecedor, string Situacao, Guid ResponsavelId, string? ResponsavelNome, string OrigemFisica, string Destino, string Motivo, string EsferaGoverno, string TipoEntidade, string UnidadeGestora, string UnidadeExecutora, string AbrangenciaTerritorial, long Version, DateTimeOffset CriadaEm, DateTimeOffset? ExpedidaEm, DateTimeOffset? EntregueEm, DateTimeOffset? CanceladaEm, string? Modalidade, string? ReferenciaTransporte, string? RecebedorOuReferencia, string? DocumentoProtocolo);
    private sealed record DestinacaoRow(long RecebimentoItemId, string Produto, string Unidade, decimal QuantidadeRejeitada, decimal QuantidadeReservada, decimal QuantidadeExpedidaNaoEntregue, decimal QuantidadeEntregue, decimal SaldoSemDestinacao);
}
