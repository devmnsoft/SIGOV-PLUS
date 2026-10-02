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

/// <summary>
/// Bloco B da jornada procure-to-pay: cotação só de requisição aprovada com saldo transacional
/// por item (derivation do próprio banco), convites a fornecedores ativos, respostas registradas
/// internamente pelo operador autorizado, comparativo por fórmula explícita e seleção humana que
/// gera pedidos de forma atômica e idempotente, reconhecidos pelo recebimento existente.
/// </summary>
public sealed class CotacaoCompraRepository(NpgsqlConnectionFactory factory) : ICotacaoCompraRepository
{
    /// <summary>
    /// Saldo reservado por item da requisição: soma das quantidades em cotações ABERTA/EM_RESPOSTA
    /// mais, nas SELECIONADAS, somente os itens efetivamente selecionados. Exige a tabela
    /// requisicao_item com alias <c>i</c> em escopo. Derivada e transacional — nunca armazenada.
    /// </summary>
    private const string ReservaPorItem = "coalesce((select sum(ci.quantidade) from sigov.compras_empresarial_cotacao_item ci join sigov.compras_empresarial_cotacao cc on cc.id=ci.cotacao_id and cc.tenant_id=ci.tenant_id where ci.tenant_id=i.tenant_id and ci.requisicao_item_id=i.id and ((cc.status in ('ABERTA','EM_RESPOSTA')) or (cc.status='SELECIONADA' and exists(select 1 from sigov.compras_empresarial_cotacao_selecao cs where cs.tenant_id=cc.tenant_id and cs.cotacao_id=cc.id and cs.requisicao_item_id=i.id)))),0)";

    private static Task LockAsync(NpgsqlConnection cn, NpgsqlTransaction tx, ComprasContext x, string rotulo, CancellationToken ct)
        => cn.ExecuteAsync(new CommandDefinition("select pg_advisory_xact_lock(hashtextextended(@chaveLock,0))", new { chaveLock = $"{x.TenantId:D}|COTACAO|{rotulo}" }, tx, cancellationToken: ct));

    private static string Sha256(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    public async Task<PagedResult<CotacaoResumo>> ListarAsync(ComprasContext x, CotacaoFiltro f, CancellationToken ct)
    {
        var page = Math.Max(1, f.Pagina); var size = Math.Clamp(f.Tamanho, 1, 100);
        var q = @"select count(*) from sigov.compras_empresarial_cotacao co where co.tenant_id=@t and not co.is_deleted and (@status is null or co.status=@status) and (@rq is null or co.requisicao_id=@rq);
select co.id Id,co.numero Numero,co.rodada Rodada,co.requisicao_id RequisicaoId,coalesce(rr.numero,'indisponivel') NumeroRequisicao,co.status Status,co.prazo Prazo,co.created_at CriadoEm,co.version Version,
(select count(*) from sigov.compras_empresarial_cotacao_item ci where ci.cotacao_id=co.id and ci.tenant_id=co.tenant_id)::int Itens,
(select count(*) from sigov.compras_empresarial_cotacao_convite cv where cv.cotacao_id=co.id and cv.tenant_id=co.tenant_id and cv.status in ('PENDENTE','RESPONDIDO'))::int ConvitesAtivos,
(select count(distinct ri.convite_id) from sigov.compras_empresarial_cotacao_resposta_item ri where ri.tenant_id=co.tenant_id and exists(select 1 from sigov.compras_empresarial_cotacao_convite xc where xc.id=ri.convite_id and xc.cotacao_id=co.id))::int Respostas
from sigov.compras_empresarial_cotacao co
left join sigov.compras_empresarial_requisicao rr on rr.id=co.requisicao_id and rr.tenant_id=co.tenant_id
where co.tenant_id=@t and not co.is_deleted and (@status is null or co.status=@status) and (@rq is null or co.requisicao_id=@rq)
order by co.created_at desc,co.id desc offset @off limit @s";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, status = f.Status, rq = f.RequisicaoId, off = (long)(page - 1) * size, s = size }, cancellationToken: ct));
        var total = await m.ReadSingleAsync<long>();
        return new((await m.ReadAsync<CotacaoResumo>()).AsList(), page, size, total);
    }

    public async Task<PagedResult<CotacaoElegivelResumo>> ListarElegiveisAsync(ComprasContext x, string? busca, int pagina, int tamanho, CancellationToken ct)
    {
        var page = Math.Max(1, pagina); var size = Math.Clamp(tamanho, 1, 100);
        const string baseWhere = @"r.tenant_id=@t and r.status='APROVADA' and not r.is_deleted
and not exists(select 1 from sigov.compras_empresarial_cotacao co where co.tenant_id=r.tenant_id and co.requisicao_id=r.id and not co.is_deleted and co.status in ('ABERTA','EM_RESPOSTA'))
and (@busca is null or r.numero ilike @term or coalesce(r.setor,'') ilike @term)";
        string saldoItens = $@"select 1 from sigov.compras_empresarial_requisicao_item i where i.tenant_id=r.tenant_id and i.requisicao_id=r.id and not i.is_deleted and i.quantidade>({ReservaPorItem})";
        var q = $@"select count(*) from sigov.compras_empresarial_requisicao r where {baseWhere} and exists({saldoItens});
select r.id RequisicaoId,r.numero Numero,r.setor Setor,r.urgencia Urgencia,r.valor_estimado ValorEstimado,r.data_necessaria DataNecessaria,
(select count(*) from sigov.compras_empresarial_requisicao_item i where i.tenant_id=r.tenant_id and i.requisicao_id=r.id and not i.is_deleted and i.quantidade>({ReservaPorItem}))::int ItensElegiveis,
(select coalesce(sum(i.quantidade-({ReservaPorItem})),0) from sigov.compras_empresarial_requisicao_item i where i.tenant_id=r.tenant_id and i.requisicao_id=r.id and not i.is_deleted) SaldoDisponivelTotal
from sigov.compras_empresarial_requisicao r where {baseWhere} and exists({saldoItens})
order by r.created_at desc,r.id desc offset @off limit @s";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, busca, term = busca is null ? null : $"%{busca}%", off = (long)(page - 1) * size, s = size }, cancellationToken: ct));
        var total = await m.ReadSingleAsync<long>();
        return new((await m.ReadAsync<CotacaoElegivelResumo>()).AsList(), page, size, total);
    }

    public async Task<CotacaoElaboracaoViewModel?> ObterElaboracaoAsync(ComprasContext x, Guid requisicaoId, CancellationToken ct)
    {
        var q = $@"select r.id Id,r.numero Numero,r.status Status,r.urgencia Urgencia,r.setor Setor,r.valor_estimado ValorEstimado from sigov.compras_empresarial_requisicao r where r.tenant_id=@t and r.id=@id and not r.is_deleted;
select i.id Id,i.ordem Ordem,i.tipo Tipo,i.descricao Descricao,i.especificacao Especificacao,i.unidade Unidade,i.quantidade Quantidade,({ReservaPorItem}) Reserva,i.exige_inspecao ExigeInspecao from sigov.compras_empresarial_requisicao_item i where i.tenant_id=@t and i.requisicao_id=@id and not i.is_deleted order by i.ordem,i.id;
select co.id Id,co.numero Numero,co.rodada Rodada,co.requisicao_id RequisicaoId,coalesce(rr.numero,'indisponivel') NumeroRequisicao,co.status Status,co.prazo Prazo,co.created_at CriadoEm,co.version Version,
(select count(*) from sigov.compras_empresarial_cotacao_item ci where ci.cotacao_id=co.id and ci.tenant_id=co.tenant_id)::int Itens,
(select count(*) from sigov.compras_empresarial_cotacao_convite cv where cv.cotacao_id=co.id and cv.tenant_id=co.tenant_id and cv.status in ('PENDENTE','RESPONDIDO'))::int ConvitesAtivos,
(select count(distinct ri.convite_id) from sigov.compras_empresarial_cotacao_resposta_item ri where ri.tenant_id=co.tenant_id and exists(select 1 from sigov.compras_empresarial_cotacao_convite xc where xc.id=ri.convite_id and xc.cotacao_id=co.id))::int Respostas
from sigov.compras_empresarial_cotacao co left join sigov.compras_empresarial_requisicao rr on rr.id=co.requisicao_id and rr.tenant_id=co.tenant_id
where co.tenant_id=@t and co.requisicao_id=@id and not co.is_deleted order by co.rodada desc limit 20";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, id = requisicaoId }, cancellationToken: ct));
        var req = await m.ReadSingleOrDefaultAsync<ElabRequisicao>();
        if (req is null) return null;
        var itensBrutos = (await m.ReadAsync<ElabItem>()).AsList();
        var itens = itensBrutos.Select(i => new CotacaoElaboracaoItem(i.Id, i.Ordem, i.Tipo, i.Descricao, i.Especificacao, i.Unidade, i.Quantidade, i.Reserva, i.Quantidade - i.Reserva, i.ExigeInspecao)).AsList();
        var relacionadas = (await m.ReadAsync<CotacaoResumo>()).AsList();
        var ativa = relacionadas.Any(c => c.Status is "ABERTA" or "EM_RESPOSTA");
        var elegiveis = itens.Count(i => i.SaldoDisponivel > 0m);
        bool elegivel = req.Status == "APROVADA" && !ativa && elegiveis > 0;
        string? motivo = req.Status != "APROVADA"
            ? $"A requisição está na situação {req.Status}; somente requisições APROVADAS geram cotação."
            : ativa ? "Já existe cotação ativa (ABERTA ou EM_RESPOSTA) para esta requisição."
            : elegiveis == 0 ? "Todos os itens já estão integralmente reservados por rodadas anteriores." : null;
        return new(requisicaoId, req.Numero, req.Status, req.Urgencia, req.Setor, req.ValorEstimado, elegivel, motivo, relacionadas.Count == 0 ? 1 : relacionadas.Max(c => c.Rodada) + 1, itens, relacionadas);
    }

    public async Task<IReadOnlyList<CotacaoProdutoResumo>> PesquisarProdutosAsync(ComprasContext x, string? busca, int limite, CancellationToken ct)
    {
        const string q = "select id Id,sku Sku,nome Nome,unidade Unidade from sigov.estoque_produto where tenant_id=@t and ativo and (@busca is null or nome ilike @term or sku ilike @term) order by nome limit @lim";
        await using var cn = factory.CreateConnection();
        return (await cn.QueryAsync<CotacaoProdutoResumo>(new CommandDefinition(q, new { t = x.TenantId, busca, term = busca is null ? null : $"%{busca}%", lim = Math.Clamp(limite, 1, 50) }, cancellationToken: ct))).AsList();
    }

    public async Task<CotacaoCriaResultado> CriarAsync(ComprasContext x, CriarCotacaoRequest r, string key, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(cn, tx, x, $"REQUISICAO|{r.RequisicaoId:D}|CRIAR", ct);
            var hash = FornecedorRepository.HashPayload(new
            {
                r.RequisicaoId,
                fornecedores = r.FornecedorIds.Distinct().OrderBy(v => v.ToString("D")).ToArray(),
                prazo = r.Prazo.ToString("O"),
                produtos = r.Produtos.OrderBy(p => p.RequisicaoItemId.ToString("D")).Select(p => new { p.RequisicaoItemId, p.ProdutoId }).ToArray()
            });
            var anterior = await cn.QuerySingleOrDefaultAsync<FornecedorRepository.IdemComHash>(new CommandDefinition("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_CRIAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior.RequestHash is not null && anterior.RequestHash != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var resultado = await cn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition("select coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_CRIAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
                if (!string.IsNullOrWhiteSpace(resultado))
                {
                    using var doc = JsonDocument.Parse(resultado);
                    var ro = doc.RootElement;
                    await tx.CommitAsync(ct);
                    return new(ro.GetProperty("id").GetGuid(), ro.GetProperty("numero").GetString()!, ro.GetProperty("rodada").GetInt32(), ro.GetProperty("itens").GetInt32(), ro.GetProperty("convites").GetInt32(), DateTime.Parse(ro.GetProperty("prazo").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), true);
                }
                // Legado sem snapshot: re-derive explicitamente da rodada mais recente vinculada.
                var estado = await cn.QuerySingleOrDefaultAsync<EstadoCotacao?>(new CommandDefinition("select id Id,numero Numero,rodada Rodada,prazo Prazo,(select count(*) from sigov.compras_empresarial_cotacao_item ci where ci.cotacao_id=co.id and ci.tenant_id=co.tenant_id)::int Itens,(select count(*) from sigov.compras_empresarial_cotacao_convite cv where cv.cotacao_id=co.id and cv.tenant_id=co.tenant_id)::int Convites from sigov.compras_empresarial_cotacao co where co.tenant_id=@t and co.requisicao_id=@rq order by co.rodada desc limit 1", new { t = x.TenantId, rq = r.RequisicaoId }, tx, cancellationToken: ct));
                if (estado is null) throw new InvalidOperationException("Registro de idempotência sem cotação correspondente; verifique o histórico.");
                await tx.CommitAsync(ct);
                return new(estado.Id, estado.Numero, estado.Rodada, estado.Itens, estado.Convites, estado.Prazo, true);
            }

            var rq = await cn.QuerySingleOrDefaultAsync<StatusSomente?>(new CommandDefinition("select status from sigov.compras_empresarial_requisicao where tenant_id=@t and id=@id and not is_deleted for update", new { t = x.TenantId, id = r.RequisicaoId }, tx, cancellationToken: ct));
            if (rq is null) throw new KeyNotFoundException("Requisição não encontrada no contexto autorizado.");
            if (rq.Status != "APROVADA") throw new InvalidOperationException("Somente requisições aprovadas podem gerar cotação.");
            var ativa = await cn.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.compras_empresarial_cotacao where tenant_id=@t and requisicao_id=@id and not is_deleted and status in ('ABERTA','EM_RESPOSTA'))", new { t = x.TenantId, id = r.RequisicaoId }, tx, cancellationToken: ct));
            if (ativa) throw new InvalidOperationException("Já existe cotação ativa para esta requisição; conclua a seleção ou encerre a rodada antes de criar outra.");

            var linhas = (await cn.QueryAsync<CriarLinha>(new CommandDefinition($"select i.id Id,i.descricao Descricao,i.quantidade Quantidade,({ReservaPorItem}) Reserva from sigov.compras_empresarial_requisicao_item i where i.tenant_id=@t and i.requisicao_id=@id and not i.is_deleted order by i.ordem,i.id", new { t = x.TenantId, id = r.RequisicaoId }, tx, cancellationToken: ct))).AsList();
            var elegiveis = linhas.Where(l => l.Quantidade - l.Reserva > 0m).ToList();
            if (elegiveis.Count == 0) throw new InvalidOperationException("Todos os itens da requisição já estão integralmente reservados; não há saldo para nova cotação.");
            var produtoPorItem = new Dictionary<Guid, Guid>();
            foreach (var l in elegiveis)
            {
                var correspondentes = r.Produtos.Where(p => p.RequisicaoItemId == l.Id).ToList();
                if (correspondentes.Count != 1) throw new ArgumentException($"Informe exatamente um produto do catálogo para o item \"{l.Descricao}\" com saldo disponível.");
                produtoPorItem[l.Id] = correspondentes[0].ProdutoId;
            }
            foreach (var p in r.Produtos.Where(p => !elegiveis.Any(l => l.Id == p.RequisicaoItemId)))
                throw new ArgumentException("O item informado não pertence à requisição ou não possui saldo disponível para cotação.");

            var produtosInvalidos = await cn.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from (select distinct unnest(@ids) as id) u where not exists(select 1 from sigov.estoque_produto ep where ep.tenant_id=@t and ep.id=u.id and ep.ativo)", new { ids = produtoPorItem.Values.ToArray(), t = x.TenantId }, tx, cancellationToken: ct));
            if (produtosInvalidos > 0) throw new ArgumentException("Um ou mais produtos do catálogo não existem ou estão inativos neste contexto.");

            var fornecedorIds = r.FornecedorIds.Distinct().OrderBy(v => v.ToString("D")).ToArray();
            var fornecedoresValidos = await cn.ExecuteScalarAsync<int>(new CommandDefinition("select count(*) from sigov.compras_empresarial_fornecedor where tenant_id=@t and not is_deleted and status not in ('BLOQUEADO','SUSPENSO') and id=any(@ids)", new { t = x.TenantId, ids = fornecedorIds }, tx, cancellationToken: ct));
            if (fornecedoresValidos != fornecedorIds.Length) throw new ArgumentException("Um ou mais fornecedores informados não existem ou não estão habilitados neste contexto.");

            var cid = Guid.NewGuid();
            var numero = await FornecedorRepository.Next(cn, tx, x.TenantId, "COTACAO", "CT", ct);
            var rodada = await cn.ExecuteScalarAsync<int>(new CommandDefinition("select coalesce(max(rodada),0)+1 from sigov.compras_empresarial_cotacao where tenant_id=@t and requisicao_id=@id", new { t = x.TenantId, id = r.RequisicaoId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_cotacao(id,tenant_id,requisicao_id,numero,rodada,prazo,status,created_by,updated_by,correlation_id) values(@id,@t,@rq,@numero,@rodada,@prazo,'ABERTA',@us,@us,@corr)", new { id = cid, t = x.TenantId, rq = r.RequisicaoId, numero, rodada, prazo = r.Prazo, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            foreach (var l in elegiveis)
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_cotacao_item(tenant_id,cotacao_id,requisicao_item_id,produto_id,quantidade,created_by,correlation_id) values(@t,@cid,@ri,@prod,@qtd,@us,@corr)", new { t = x.TenantId, cid, ri = l.Id, prod = produtoPorItem[l.Id], qtd = l.Quantidade - l.Reserva, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            foreach (var fid in fornecedorIds)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_cotacao_convite(tenant_id,cotacao_id,fornecedor_id,token_hash,expira_em,status,created_by,updated_by,correlation_id) values(@t,@cid,@fid,@tokenHash,@prazo,'PENDENTE',@us,@us,@corr)", new { t = x.TenantId, cid, fid, tokenHash, prazo = r.Prazo, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            }
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'COTACAO',@cid,'CRIADA',jsonb_build_object('numero',@numero,'rodada',@rodada,'itens',@itens,'convites',@convites),@us,@corr)", new { t = x.TenantId, cid, numero, rodada, itens = elegiveis.Count, convites = fornecedorIds.Length, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'REQUISICAO',@rq,'COTACAO_ABERTA',jsonb_build_object('cotacao_id',@cid::text,'numero',@numero),@us,@corr)", new { t = x.TenantId, rq = r.RequisicaoId, cid, numero, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash,resultado) values(@t,'COTACAO_CRIAR',@key,@cid,@hash,jsonb_build_object('id',@cid::text,'numero',@numero,'rodada',@rodada,'itens',@itens,'convites',@convites,'prazo',@prazo))", new { t = x.TenantId, key, cid, hash, numero, rodada, itens = elegiveis.Count, convites = fornecedorIds.Length, prazo = r.Prazo }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new(cid, numero, rodada, elegiveis.Count, fornecedorIds.Length, r.Prazo, false);
        }
        catch
        { try { await tx.RollbackAsync(ct); } catch (InvalidOperationException) { } throw; }
    }

    public async Task<CotacaoDetalhe?> ObterAsync(ComprasContext x, Guid id, CancellationToken ct)
    {
        var q = $@"select co.id Id,co.numero Numero,co.rodada Rodada,co.requisicao_id RequisicaoId,coalesce(rr.numero,'indisponivel') NumeroRequisicao,co.status Status,co.prazo Prazo,co.created_at CriadoEm,co.selecionado_em SelecionadoEm,co.version Version from sigov.compras_empresarial_cotacao co left join sigov.compras_empresarial_requisicao rr on rr.id=co.requisicao_id and rr.tenant_id=co.tenant_id where co.tenant_id=@t and co.id=@id and not co.is_deleted;
select ci.id Id,ci.requisicao_item_id RequisicaoItemId,ri.ordem Ordem,ri.descricao Descricao,ri.especificacao Especificacao,ri.unidade Unidade,ci.quantidade Quantidade,ci.produto_id ProdutoId,ep.nome ProdutoNome,ep.sku ProdutoSku from sigov.compras_empresarial_cotacao_item ci join sigov.compras_empresarial_requisicao_item ri on ri.id=ci.requisicao_item_id and ri.tenant_id=ci.tenant_id join sigov.estoque_produto ep on ep.id=ci.produto_id and ep.tenant_id=ci.tenant_id where ci.tenant_id=@t and ci.cotacao_id=@id order by ri.ordem,ri.id;
select cv.id Id,cv.fornecedor_id FornecedorId,coalesce(f.nome_fantasia,f.razao_social) FornecedorNome,f.documento_mascarado FornecedorDocumento,cv.status Status,cv.expira_em ExpiraEm,cv.version Version,cv.respondido_em Responder,(select count(*) from sigov.compras_empresarial_cotacao_resposta_item ri2 where ri2.convite_id=cv.id and ri2.tenant_id=cv.tenant_id)::int ItensRespondidos from sigov.compras_empresarial_cotacao_convite cv join sigov.compras_empresarial_fornecedor f on f.id=cv.fornecedor_id and f.tenant_id=cv.tenant_id where cv.tenant_id=@t and cv.cotacao_id=@id order by f.razao_social;
select cs.id Id,cs.requisicao_item_id RequisicaoItemId,ri.descricao Descricao,ri.unidade Unidade,cs.quantidade Quantidade,cs.fornecedor_id FornecedorId,coalesce(f.nome_fantasia,f.razao_social) FornecedorNome,cs.custo_total_item CustoTotalItem,cs.justificativa Justificativa from sigov.compras_empresarial_cotacao_selecao cs join sigov.compras_empresarial_requisicao_item ri on ri.id=cs.requisicao_item_id and ri.tenant_id=cs.tenant_id left join sigov.compras_empresarial_fornecedor f on f.id=cs.fornecedor_id and f.tenant_id=cs.tenant_id where cs.tenant_id=@t and cs.cotacao_id=@id order by ri.ordem,ri.id;
select p.id PedidoId,p.numero Numero,p.fornecedor_id FornecedorId,coalesce(f.nome_fantasia,f.razao_social) FornecedorNome,p.valor_total ValorTotal,(select count(*) from sigov.compras_empresarial_pedido_item pi where pi.pedido_id=p.id and pi.tenant_id=p.tenant_id)::int Itens from sigov.compras_empresarial_pedido p left join sigov.compras_empresarial_fornecedor f on f.id=p.fornecedor_id and f.tenant_id=p.tenant_id where p.tenant_id=@t and p.cotacao_id=@id and not p.is_deleted order by p.created_at,p.id";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, id }, cancellationToken: ct));
        var h = await m.ReadSingleOrDefaultAsync<CotacaoHeader>();
        if (h is null) return null;
        var itens = (await m.ReadAsync<CotacaoItemDetalhe>()).AsList();
        var agora = DateTime.UtcNow;
        var convites = (await m.ReadAsync<ConviteRaw>()).AsList()
            .Select(c => new CotacaoConviteDetalhe(c.Id, c.FornecedorId, c.FornecedorNome, c.FornecedorDocumento, c.Status == "PENDENTE" && c.ExpiraEm <= agora ? "EXPIRADO" : c.Status, c.ExpiraEm, c.Version, c.Responder, c.ItensRespondidos)).AsList();
        IReadOnlyList<CotacaoSelecaoLinha>? selecoes = null; IReadOnlyList<CotacaoPedidoGerado>? pedidos = null;
        if (h.Status == "SELECIONADA")
        {
            selecoes = (await m.ReadAsync<CotacaoSelecaoLinha>()).AsList();
            pedidos = (await m.ReadAsync<CotacaoPedidoGerado>()).AsList();
        }
        return new(h.Id, h.Numero, h.Rodada, h.RequisicaoId, h.NumeroRequisicao, h.Status, h.Prazo, h.CriadoEm, h.SelecionadoEm, h.Version, itens, convites, selecoes, pedidos);
    }

    public async Task<CotacaoComparativoViewModel?> ObterComparativoAsync(ComprasContext x, Guid id, CancellationToken ct)
    {
        var q = @"select co.id CotacaoId,co.numero Numero,co.status Status,co.prazo Prazo,co.version Version from sigov.compras_empresarial_cotacao co where co.tenant_id=@t and co.id=@id and not co.is_deleted;
select ci.requisicao_item_id RequisicaoItemId,ri.ordem Ordem,ri.descricao Descricao,ri.especificacao Especificacao,ri.unidade Unidade,ci.quantidade Quantidade from sigov.compras_empresarial_cotacao_item ci join sigov.compras_empresarial_requisicao_item ri on ri.id=ci.requisicao_item_id and ri.tenant_id=ci.tenant_id where ci.tenant_id=@t and ci.cotacao_id=@id order by ri.ordem,ri.id;
select ri.convite_id ConviteId,cv.fornecedor_id FornecedorId,coalesce(f.nome_fantasia,f.razao_social) FornecedorNome,ri.requisicao_item_id RequisicaoItemId,ri.preco_unitario PrecoUnitario,ri.desconto Desconto,ri.imposto Imposto,ri.frete Frete,ri.prazo_dias PrazoDias,ri.marca Marca,ri.fabricante Fabricante,ri.recusado Recusado
from sigov.compras_empresarial_cotacao_resposta_item ri
join sigov.compras_empresarial_cotacao_convite cv on cv.id=ri.convite_id and cv.tenant_id=ri.tenant_id
join sigov.compras_empresarial_cotacao co on co.id=cv.cotacao_id and co.tenant_id=cv.tenant_id
join sigov.compras_empresarial_fornecedor f on f.id=cv.fornecedor_id and f.tenant_id=cv.tenant_id
where co.tenant_id=@t and co.id=@id";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, id }, cancellationToken: ct));
        var h = await m.ReadSingleOrDefaultAsync<ComparativoHeader>();
        if (h is null) return null;
        var linhas = (await m.ReadAsync<ComparativoLinhaRow>()).AsList();
        var ofertas = (await m.ReadAsync<OfertaRaw>()).AsList();
        var result = linhas.Select(l =>
        {
            var ofertasDoItem = ofertas.Where(o => o.RequisicaoItemId == l.RequisicaoItemId).Select(o =>
            {
                var cue = Math.Round(o.PrecoUnitario * (1m + o.Imposto / 100m - o.Desconto / 100m), 2, MidpointRounding.AwayFromZero);
                var custo = Math.Round(cue * l.Quantidade + o.Frete, 2, MidpointRounding.AwayFromZero);
                var validas = ofertas.Where(v => v.RequisicaoItemId == l.RequisicaoItemId && !v.Recusado).Select(v => Math.Round(Math.Round(v.PrecoUnitario * (1m + v.Imposto / 100m - v.Desconto / 100m), 2, MidpointRounding.AwayFromZero) * l.Quantidade + v.Frete, 2, MidpointRounding.AwayFromZero)).ToList();
                decimal menor = validas.Count > 0 ? validas.Min() : 0m;
                var empate = validas.Count(v => v == menor) >= 2;
                return new CotacaoComparativoOferta(o.ConviteId, o.FornecedorId, o.FornecedorNome, o.PrecoUnitario, o.Desconto, o.Imposto, o.Frete, cue, custo, o.PrazoDias, o.Marca, o.Fabricante, o.Recusado, !o.Recusado && custo == menor, !o.Recusado && empate && custo == menor);
            }).OrderBy(o => o.CustoTotalItem).AsList();
            return new CotacaoComparativoLinha(l.RequisicaoItemId, l.Ordem, l.Descricao, l.Especificacao, l.Unidade, l.Quantidade, ofertasDoItem, ofertasDoItem.Count == 0);
        }).AsList();
        return new(h.CotacaoId, h.Numero, h.Status, h.Prazo, h.Version, FormulaComparacaoCotacao.Texto, h.Status == "EM_RESPOSTA", result);
    }

    public async Task<RespostaRegistradaResultado> RegistrarRespostaAsync(ComprasContext x, Guid cotacaoId, RegistrarRespostaRequest r, string key, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(cn, tx, x, $"{cotacaoId:D}|RESPOSTA", ct);
            var hash = FornecedorRepository.HashPayload(new
            {
                cotacaoId,
                r.ConviteId,
                itens = r.Itens.OrderBy(i => i.RequisicaoItemId.ToString("D")).Select(i => new { i.RequisicaoItemId, i.PrecoUnitario, i.Desconto, i.Imposto, i.Frete, i.PrazoDias, marca = i.Marca?.Trim(), fabricante = i.Fabricante?.Trim(), i.Recusado }).ToArray()
            });
            var anterior = await cn.QuerySingleOrDefaultAsync<FornecedorRepository.IdemComHash>(new CommandDefinition("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_RESPONDER' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior.RequestHash is not null && anterior.RequestHash != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var resultado = await cn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition("select coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_RESPONDER' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
                if (!string.IsNullOrWhiteSpace(resultado))
                {
                    using var doc = JsonDocument.Parse(resultado);
                    var ro = doc.RootElement;
                    await tx.CommitAsync(ct);
                    return new(ro.GetProperty("id").GetGuid(), ro.GetProperty("convite").GetGuid(), ro.GetProperty("convite_status").GetString()!, ro.GetProperty("cotacao_status").GetString()!, true);
                }
                var estado = await cn.QuerySingleOrDefaultAsync<(string CotacaoStatus, string ConviteStatus)?>(new CommandDefinition("select co.status CotacaoStatus,c.status ConviteStatus from sigov.compras_empresarial_cotacao_convite c join sigov.compras_empresarial_cotacao co on co.id=c.cotacao_id and co.tenant_id=c.tenant_id where c.tenant_id=@t and c.id=@cv", new { t = x.TenantId, cv = r.ConviteId }, tx, cancellationToken: ct));
                if (estado is null) throw new InvalidOperationException("Registro de idempotência sem convite correspondente; verifique o histórico.");
                await tx.CommitAsync(ct);
                return new(cotacaoId, r.ConviteId, estado.Value.ConviteStatus, estado.Value.CotacaoStatus, true);
            }

            var convite = await cn.QuerySingleOrDefaultAsync<ConviteLock>(new CommandDefinition("select fornecedor_id FornecedorId,status,version,expira_em ExpiraEm from sigov.compras_empresarial_cotacao_convite where tenant_id=@t and id=@id and cotacao_id=@co for update", new { t = x.TenantId, id = r.ConviteId, co = cotacaoId }, tx, cancellationToken: ct));
            if (convite is null) throw new KeyNotFoundException("Convite não encontrado no contexto autorizado.");
            if (convite.Status == "REVOCADO") throw new InvalidOperationException("O convite foi revogado e não pode mais receber respostas.");
            if (convite.Status == "PENDENTE" && convite.ExpiraEm <= DateTime.UtcNow) throw new InvalidOperationException("O convite expirou antes do registro da resposta.");
            var co = await cn.QuerySingleOrDefaultAsync<CotacaoLock>(new CommandDefinition("select requisicao_id RequisicaoId,status,version,selecionado_em SelecionadoEm from sigov.compras_empresarial_cotacao where tenant_id=@t and id=@id for update", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct));
            if (co is null) throw new KeyNotFoundException("Cotação não encontrada no contexto autorizado.");
            if (co.Status is not ("ABERTA" or "EM_RESPOSTA")) throw new InvalidOperationException("A cotação não está mais aberta para respostas.");
            if (convite.Version != r.ConviteVersion) throw new ComprasConcurrencyException("Versão desatualizada; recarregue o detalhe da cotação e tente novamente.");

            var esperados = (await cn.QueryAsync<Guid>(new CommandDefinition("select requisicao_item_id from sigov.compras_empresarial_cotacao_item where tenant_id=@t and cotacao_id=@id", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct))).ToHashSet();
            var informados = r.Itens.Select(i => i.RequisicaoItemId).ToHashSet();
            if (esperados.Count != informados.Count || !esperados.IsSubsetOf(informados))
                throw new ArgumentException("Informe uma linha de resposta para cada item da cotação (recuse explicitamente quando aplicável).");

            foreach (var i in r.Itens)
                await cn.ExecuteAsync(new CommandDefinition(@"insert into sigov.compras_empresarial_cotacao_resposta_item(tenant_id,convite_id,requisicao_item_id,preco_unitario,desconto,imposto,frete,prazo_dias,marca,fabricante,recusado,created_by,updated_by,correlation_id)
values(@t,@cv,@ri,@pu,@des,@imp,@frete,@prazo,@marca,@fab,@rec,@us,@us,@corr)
on conflict (tenant_id,convite_id,requisicao_item_id) do update set preco_unitario=@pu,desconto=@des,imposto=@imp,frete=@frete,prazo_dias=@prazo,marca=@marca,fabricante=@fab,recusado=@rec,updated_at=now(),updated_by=@us,correlation_id=@corr,version=sigov.compras_empresarial_cotacao_resposta_item.version+1",
                    new { t = x.TenantId, cv = r.ConviteId, ri = i.RequisicaoItemId, pu = i.PrecoUnitario, des = i.Desconto, imp = i.Imposto, frete = i.Frete, prazo = i.PrazoDias, marca = i.Marca?.Trim(), fab = i.Fabricante?.Trim(), rec = i.Recusado, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_cotacao_convite set status='RESPONDIDO',respondido_em=coalesce(respondido_em,now()),updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and id=@id", new { t = x.TenantId, id = r.ConviteId, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_cotacao set status='EM_RESPOSTA',updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and id=@id and status='ABERTA'", new { t = x.TenantId, id = cotacaoId, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'COTACAO',@co,'RESPOSTA_REGISTRADA',jsonb_build_object('convite_id',@cv::text,'fornecedor_id',@fid::text,'itens',@itens),@us,@corr)", new { t = x.TenantId, co = cotacaoId, cv = r.ConviteId, fid = convite.FornecedorId, itens = r.Itens.Count, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash,resultado) values(@t,'COTACAO_RESPONDER',@key,@cv,@hash,jsonb_build_object('id',@co::text,'convite',@cv::text,'convite_status','RESPONDIDO','cotacao_status','EM_RESPOSTA'))", new { t = x.TenantId, key, cv = r.ConviteId, co = cotacaoId, hash }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new(cotacaoId, r.ConviteId, "RESPONDIDO", "EM_RESPOSTA", false);
        }
        catch
        { try { await tx.RollbackAsync(ct); } catch (InvalidOperationException) { } throw; }
    }

    public async Task<SelecaoConcluidaResultado> SelecionarAsync(ComprasContext x, Guid cotacaoId, SelecionarItensRequest r, string key, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(cn, tx, x, $"{cotacaoId:D}|SELECAO", ct);
            var hash = FornecedorRepository.HashPayload(new
            {
                cotacaoId,
                r.Version,
                itens = r.Itens.OrderBy(i => i.RequisicaoItemId.ToString("D")).Select(i => new { i.RequisicaoItemId, i.ConviteId, justificativa = i.Justificativa?.Trim() }).ToArray()
            });
            var anterior = await cn.QuerySingleOrDefaultAsync<FornecedorRepository.IdemComHash>(new CommandDefinition("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_SELECIONAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior.RequestHash is not null && anterior.RequestHash != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var resultado = await cn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition("select coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_SELECIONAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
                if (!string.IsNullOrWhiteSpace(resultado))
                {
                    using var doc = JsonDocument.Parse(resultado);
                    var ro = doc.RootElement;
                    var pedidosReplay = ro.GetProperty("pedidos").EnumerateArray().Select(p => new CotacaoPedidoGerado(Guid.Parse(p.GetProperty("id").GetString()!), p.GetProperty("numero").GetString()!, Guid.Parse(p.GetProperty("fornecedor_id").GetString()!), p.GetProperty("fornecedor_nome").GetString()!, p.GetProperty("valor_total").GetDecimal(), p.GetProperty("itens").GetInt32())).AsList();
                    await tx.CommitAsync(ct);
                    return new(ro.GetProperty("id").GetGuid(), "SELECIONADA", DateTime.Parse(ro.GetProperty("selecionado_em").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), pedidosReplay, true);
                }
                var estado = await cn.QuerySingleOrDefaultAsync<CotacaoHeader?>(new CommandDefinition("select id Id,numero Numero,rodada Rodada,requisicao_id RequisicaoId,coalesce(numero,'indisponivel') NumeroRequisicao,status Status,prazo Prazo,created_at CriadoEm,selecionado_em SelecionadoEm,version Version from sigov.compras_empresarial_cotacao where tenant_id=@t and id=@id", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct));
                if (estado is null || estado.SelecionadoEm is null) throw new InvalidOperationException("Registro de idempotência sem seleção correspondente; verifique o histórico.");
                await tx.CommitAsync(ct);
                return new(cotacaoId, "SELECIONADA", estado.SelecionadoEm.Value, Array.Empty<CotacaoPedidoGerado>(), true);
            }

            var co = await cn.QuerySingleOrDefaultAsync<CotacaoLock>(new CommandDefinition("select requisicao_id RequisicaoId,status,version,selecionado_em SelecionadoEm from sigov.compras_empresarial_cotacao where tenant_id=@t and id=@id for update", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct));
            if (co is null) throw new KeyNotFoundException("Cotação não encontrada no contexto autorizado.");
            if (co.Status != "EM_RESPOSTA") throw new InvalidOperationException("A seleção é permitida apenas em cotação com respostas registradas (situação EM_RESPOSTA).");
            if (co.Version != r.Version) throw new ComprasConcurrencyException("Versão desatualizada; recarregue o comparativo e tente novamente.");

            var itens = (await cn.QueryAsync<SelecItem>(new CommandDefinition("select ci.requisicao_item_id RiId,ri.descricao Descricao,ci.quantidade Qtd,ci.produto_id Prod,ri.exige_inspecao Insp from sigov.compras_empresarial_cotacao_item ci join sigov.compras_empresarial_requisicao_item ri on ri.id=ci.requisicao_item_id and ri.tenant_id=ci.tenant_id where ci.tenant_id=@t and ci.cotacao_id=@id order by ri.ordem", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct))).AsList();
            if (itens.Count == 0) throw new InvalidOperationException("A cotação não possui itens.");
            var convites = (await cn.QueryAsync<ConviteInfo>(new CommandDefinition("select cv.id Id,cv.fornecedor_id FornecedorId,coalesce(f.nome_fantasia,f.razao_social) FornecedorNome from sigov.compras_empresarial_cotacao_convite cv join sigov.compras_empresarial_fornecedor f on f.id=cv.fornecedor_id and f.tenant_id=cv.tenant_id where cv.tenant_id=@t and cv.cotacao_id=@id and cv.status='RESPONDIDO'", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct))).ToDictionary(c => c.Id);
            var respostas = (await cn.QueryAsync<OfertaRaw>(new CommandDefinition("select ri.convite_id ConviteId,cv.fornecedor_id FornecedorId,'' FornecedorNome,ri.requisicao_item_id RequisicaoItemId,ri.preco_unitario PrecoUnitario,ri.desconto Desconto,ri.imposto Imposto,ri.frete Frete,ri.prazo_dias PrazoDias,null::text Marca,null::text Fabricante,ri.recusado Recusado from sigov.compras_empresarial_cotacao_resposta_item ri join sigov.compras_empresarial_cotacao_convite cv on cv.id=ri.convite_id and cv.tenant_id=ri.tenant_id where ri.tenant_id=@t and cv.cotacao_id=@id", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct))).AsList();

            decimal CustoEfetivo(SelecItem item, OfertaRaw oferta)
            {
                var cue = Math.Round(oferta.PrecoUnitario * (1m + oferta.Imposto / 100m - oferta.Desconto / 100m), 2, MidpointRounding.AwayFromZero);
                return Math.Round(cue * item.Qtd + oferta.Frete, 2, MidpointRounding.AwayFromZero);
            }
            var menorPorItem = new Dictionary<Guid, decimal?>();
            foreach (var i in itens)
            {
                var validas = respostas.Where(o => o.RequisicaoItemId == i.RiId && !o.Recusado).Select(o => CustoEfetivo(i, o)).ToList();
                menorPorItem[i.RiId] = validas.Count > 0 ? validas.Min() : null;
            }
            var escolhidos = new List<EscopoEscolha>();
            foreach (var e in r.Itens)
            {
                var item = itens.FirstOrDefault(i => i.RiId == e.RequisicaoItemId);
                if (item is null) throw new ArgumentException("O item informado não pertence a esta cotação.");
                if (!convites.TryGetValue(e.ConviteId, out var convite)) throw new ArgumentException("O convite informado não pertence a esta cotação ou ainda não respondeu.");
                var oferta = respostas.FirstOrDefault(o => o.ConviteId == e.ConviteId && o.RequisicaoItemId == e.RequisicaoItemId);
                if (oferta is null) throw new ArgumentException("Não há resposta registrada para este item neste fornecedor.");
                if (oferta.Recusado) throw new ArgumentException("Este item foi recusado pelo fornecedor selecionado; escolha outro.");
                var custo = CustoEfetivo(item, oferta);
                var menor = menorPorItem[e.RequisicaoItemId];
                if (menor.HasValue && Math.Round(custo, 2, MidpointRounding.AwayFromZero) > menor.Value)
                {
                    var justificativa = e.Justificativa?.Trim() ?? string.Empty;
                    if (justificativa.Length < 10) throw new ArgumentException("A seleção acima do menor custo exige justificativa com ao menos 10 caracteres.");
                }
                escolhidos.Add(new EscopoEscolha(item, e.ConviteId, convite.FornecedorId, convite.FornecedorNome, oferta, custo, e.Justificativa?.Trim()));
            }

            var pedidos = new List<CotacaoPedidoGerado>();
            var selEm = DateTime.UtcNow;
            foreach (var grupo in escolhidos.GroupBy(e => e.FornecedorId).OrderBy(g => g.Key.ToString("D")))
            {
                var pid = Guid.NewGuid();
                var numero = await FornecedorRepository.Next(cn, tx, x.TenantId, "PEDIDO", "PED", ct);
                var valor = Math.Round(grupo.Sum(e => e.Custo), 2, MidpointRounding.AwayFromZero);
                var previsao = DateTime.Today.AddDays(grupo.Max(e => e.Oferta.PrazoDias));
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_pedido(id,tenant_id,fornecedor_id,numero,status,valor_total,total,requisicao_id,cotacao_id,previsao,created_by,updated_by,correlation_id) values(@id,@t,@fid,@numero,'CONFIRMADO',@valor,@valor,@rq,@cid,@previsao,@us,@us,@corr)", new { id = pid, t = x.TenantId, fid = grupo.Key, numero, valor, rq = co.RequisicaoId, cid = cotacaoId, previsao, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
                foreach (var e in grupo)
                    await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_pedido_item(tenant_id,pedido_id,produto_id,quantidade,valor_unitario,exige_inspecao) values(@t,@pid,@prod,@qtd,@vue,@insp)", new { t = x.TenantId, pid, prod = e.Item.Prod, qtd = e.Item.Qtd, vue = Math.Round(e.Oferta.PrecoUnitario * (1m + e.Oferta.Imposto / 100m - e.Oferta.Desconto / 100m), 2, MidpointRounding.AwayFromZero), insp = e.Item.Insp }, tx, cancellationToken: ct));
                pedidos.Add(new(pid, numero, grupo.Key, grupo.First().FornecedorNome, valor, grupo.Count()));
            }
            foreach (var e in escolhidos)
            {
                var cue = Math.Round(e.Oferta.PrecoUnitario * (1m + e.Oferta.Imposto / 100m - e.Oferta.Desconto / 100m), 2, MidpointRounding.AwayFromZero);
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_cotacao_selecao(tenant_id,cotacao_id,convite_id,fornecedor_id,requisicao_item_id,produto_id,quantidade,preco_unitario_efetivo,custo_total_item,menor_custo,justificativa,criado_por,correlation_id) values(@t,@cid,@cv,@fid,@ri,@prod,@qtd,@cue,@custo,@menor,@just,@us,@corr)", new { t = x.TenantId, cid = cotacaoId, cv = e.ConviteId, fid = e.FornecedorId, ri = e.Item.RiId, prod = e.Item.Prod, qtd = e.Item.Qtd, cue, custo = e.Custo, menor = menorPorItem[e.Item.RiId] ?? e.Custo, just = e.Justificativa, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            }
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_cotacao set status='SELECIONADA',selecionado_em=@selEm,selecionado_por=@us,updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and id=@id", new { t = x.TenantId, id = cotacaoId, selEm, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'COTACAO',@cid,'SELECAO_REGISTRADA',jsonb_build_object('itens',@itens,'fornecedores',@fornecedores),@us,@corr)", new { t = x.TenantId, cid = cotacaoId, itens = escolhidos.Count, fornecedores = pedidos.Count, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            foreach (var p in pedidos)
                await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'PEDIDO',@pid,'CRIADO',jsonb_build_object('numero',@numero,'cotacao_id',@cid::text,'origem','SELECAO_COTACAO'),@us,@corr)", new { t = x.TenantId, pid = p.PedidoId, numero = p.Numero, cid = cotacaoId, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            var pedJson = JsonSerializer.Serialize(pedidos.Select(p => new { id = p.PedidoId.ToString("D"), numero = p.Numero, fornecedor_id = p.FornecedorId.ToString("D"), fornecedor_nome = p.FornecedorNome, valor_total = p.ValorTotal, itens = p.Itens }));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash,resultado) values(@t,'COTACAO_SELECIONAR',@key,@cid,@hash,jsonb_build_object('id',@cid::text,'selecionado_em',@selEm,'pedidos',cast(@pedJson as jsonb)))", new { t = x.TenantId, key, cid = cotacaoId, hash, selEm, pedJson }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new(cotacaoId, "SELECIONADA", selEm, pedidos, false);
        }
        catch
        { try { await tx.RollbackAsync(ct); } catch (InvalidOperationException) { } throw; }
    }

    public async Task<CotacaoEncerradaResultado> EncerrarAsync(ComprasContext x, Guid cotacaoId, EncerrarCotacaoRequest r, string key, CancellationToken ct)
    {
        await using var cn = factory.CreateConnection();
        await cn.OpenAsync(ct);
        await using var tx = await cn.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(cn, tx, x, $"{cotacaoId:D}|ENCERRAR", ct);
            var hash = FornecedorRepository.HashPayload(new { cotacaoId, r.Version, r.Motivo });
            var anterior = await cn.QuerySingleOrDefaultAsync<FornecedorRepository.IdemComHash>(new CommandDefinition("select recurso_id RecursoId,request_hash RequestHash from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_ENCERRAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
            if (anterior is not null)
            {
                if (anterior.RequestHash is not null && anterior.RequestHash != hash) throw new ComprasConcurrencyException("A chave de idempotência já foi usada com conteúdo diferente.");
                var resultado = await cn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition("select coalesce(resultado::text,'') from sigov.compras_empresarial_idempotencia where tenant_id=@t and operacao='COTACAO_ENCERRAR' and chave=@key", new { t = x.TenantId, key }, tx, cancellationToken: ct));
                if (!string.IsNullOrWhiteSpace(resultado))
                {
                    using var doc = JsonDocument.Parse(resultado);
                    var ro = doc.RootElement;
                    await tx.CommitAsync(ct);
                    return new(ro.GetProperty("id").GetGuid(), ro.GetProperty("status").GetString()!, true);
                }
                var estado = await cn.QuerySingleOrDefaultAsync<StatusSomente?>(new CommandDefinition("select status from sigov.compras_empresarial_cotacao where tenant_id=@t and id=@id", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct));
                if (estado is null) throw new InvalidOperationException("Registro de idempotência sem cotação correspondente; verifique o histórico.");
                await tx.CommitAsync(ct);
                return new(cotacaoId, estado.Status, true);
            }

            var co = await cn.QuerySingleOrDefaultAsync<CotacaoLock>(new CommandDefinition("select requisicao_id RequisicaoId,status,version,selecionado_em SelecionadoEm from sigov.compras_empresarial_cotacao where tenant_id=@t and id=@id for update", new { t = x.TenantId, id = cotacaoId }, tx, cancellationToken: ct));
            if (co is null) throw new KeyNotFoundException("Cotação não encontrada no contexto autorizado.");
            if (co.Status is not ("ABERTA" or "EM_RESPOSTA")) throw new InvalidOperationException("Apenas cotações abertas ou em resposta podem ser encerradas.");
            if (co.Version != r.Version) throw new ComprasConcurrencyException("Versão desatualizada; recarregue o detalhe da cotação e tente novamente.");
            await cn.ExecuteAsync(new CommandDefinition("update sigov.compras_empresarial_cotacao set status='ENCERRADA',updated_at=now(),updated_by=@us,correlation_id=@corr,version=version+1 where tenant_id=@t and id=@id", new { t = x.TenantId, id = cotacaoId, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_historico(tenant_id,aggregate_type,aggregate_id,acao,detalhes,created_by,correlation_id) values(@t,'COTACAO',@id,'ENCERRADA',jsonb_build_object('motivo',@motivo),@us,@corr)", new { t = x.TenantId, id = cotacaoId, motivo = r.Motivo, us = x.UsuarioId.ToString(), corr = x.CorrelationId }, tx, cancellationToken: ct));
            await cn.ExecuteAsync(new CommandDefinition("insert into sigov.compras_empresarial_idempotencia(tenant_id,operacao,chave,recurso_id,request_hash,resultado) values(@t,'COTACAO_ENCERRAR',@key,@id,@hash,jsonb_build_object('id',@id::text,'status','ENCERRADA'))", new { t = x.TenantId, key, id = cotacaoId, hash }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
            return new(cotacaoId, "ENCERRADA", false);
        }
        catch
        { try { await tx.RollbackAsync(ct); } catch (InvalidOperationException) { } throw; }
    }

    public async Task<PagedResult<PedidoResumo>> ListarPedidosAsync(ComprasContext x, PedidoFiltro f, CancellationToken ct)
    {
        var page = Math.Max(1, f.Pagina); var size = Math.Clamp(f.Tamanho, 1, 100);
        var q = @"select count(*) from sigov.compras_empresarial_pedido p left join sigov.compras_empresarial_fornecedor fo on fo.id=p.fornecedor_id and fo.tenant_id=p.tenant_id left join sigov.compras_empresarial_requisicao rr on rr.id=p.requisicao_id and rr.tenant_id=p.tenant_id where p.tenant_id=@t and not p.is_deleted and (@status is null or p.status=@status) and (@busca is null or p.numero ilike @term or coalesce(fo.nome_fantasia,fo.razao_social) ilike @term or coalesce(rr.numero,'') ilike @term);
select p.id Id,p.numero Numero,p.fornecedor_id FornecedorId,coalesce(fo.nome_fantasia,fo.razao_social) FornecedorNome,p.status Status,p.valor_total ValorTotal,p.previsao Previsao,p.created_at CriadoEm,p.requisicao_id RequisicaoId,p.cotacao_id CotacaoId,(select count(*) from sigov.compras_empresarial_pedido_item pi where pi.pedido_id=p.id and pi.tenant_id=p.tenant_id)::int Itens,p.version Version
from sigov.compras_empresarial_pedido p
left join sigov.compras_empresarial_fornecedor fo on fo.id=p.fornecedor_id and fo.tenant_id=p.tenant_id
left join sigov.compras_empresarial_requisicao rr on rr.id=p.requisicao_id and rr.tenant_id=p.tenant_id
where p.tenant_id=@t and not p.is_deleted and (@status is null or p.status=@status) and (@busca is null or p.numero ilike @term or coalesce(fo.nome_fantasia,fo.razao_social) ilike @term or coalesce(rr.numero,'') ilike @term)
order by p.created_at desc,p.id desc offset @off limit @s";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, status = f.Status, busca = f.Busca, term = f.Busca is null ? null : $"%{f.Busca}%", off = (long)(page - 1) * size, s = size }, cancellationToken: ct));
        var total = await m.ReadSingleAsync<long>();
        return new((await m.ReadAsync<PedidoResumo>()).AsList(), page, size, total);
    }

    public async Task<PedidoDetalhe?> ObterPedidoAsync(ComprasContext x, Guid id, CancellationToken ct)
    {
        var q = @"select p.id Id,p.numero Numero,p.status Status,p.fornecedor_id FornecedorId,coalesce(fo.nome_fantasia,fo.razao_social) FornecedorNome,p.valor_total ValorTotal,p.previsao Previsao,p.created_at CriadoEm,p.cotacao_id CotacaoId,cc.numero NumeroCotacao,p.requisicao_id RequisicaoId,rr.numero NumeroRequisicao,p.version Version
from sigov.compras_empresarial_pedido p
left join sigov.compras_empresarial_fornecedor fo on fo.id=p.fornecedor_id and fo.tenant_id=p.tenant_id
left join sigov.compras_empresarial_cotacao cc on cc.id=p.cotacao_id and cc.tenant_id=p.tenant_id
left join sigov.compras_empresarial_requisicao rr on rr.id=p.requisicao_id and rr.tenant_id=p.tenant_id
where p.tenant_id=@t and p.id=@id and not p.is_deleted;
select pi.id Id,pi.produto_id ProdutoId,ep.nome ProdutoNome,ep.unidade Unidade,pi.quantidade Quantidade,pi.quantidade_cancelada QuantidadeCancelada,pi.valor_unitario ValorUnitario,pi.exige_inspecao ExigeInspecao from sigov.compras_empresarial_pedido_item pi left join sigov.estoque_produto ep on ep.id=pi.produto_id and ep.tenant_id=pi.tenant_id where pi.tenant_id=@t and pi.pedido_id=@id order by pi.id;
select acao Acao,detalhes::text Detalhes,created_at CriadoEm from sigov.compras_empresarial_historico where tenant_id=@t and aggregate_type='PEDIDO' and aggregate_id=@id order by created_at desc,id desc limit 50";
        await using var cn = factory.CreateConnection();
        using var m = await cn.QueryMultipleAsync(new CommandDefinition(q, new { t = x.TenantId, id }, cancellationToken: ct));
        var h = await m.ReadSingleOrDefaultAsync<PedidoHeaderRow>();
        if (h is null) return null;
        var itens = (await m.ReadAsync<PedidoItemDetalhe>()).AsList();
        var historico = (await m.ReadAsync<RequisicaoHistorico>()).AsList();
        return new(h.Id, h.Numero, h.Status, h.FornecedorId, h.FornecedorNome, h.ValorTotal, h.Previsao, h.CriadoEm, h.CotacaoId, h.NumeroCotacao, h.RequisicaoId, h.NumeroRequisicao, h.Version, itens, historico);
    }

    // ===== Rows privados de leitura =====
    private sealed record ElabRequisicao(Guid Id, string Numero, string Status, string? Urgencia, string? Setor, decimal ValorEstimado);
    private sealed record ElabItem(Guid Id, int Ordem, string Tipo, string Descricao, string? Especificacao, string Unidade, decimal Quantidade, decimal Reserva, bool ExigeInspecao);
    private sealed record EstadoCotacao(Guid Id, string Numero, int Rodada, DateTime Prazo, int Itens, int Convites);
    private sealed record StatusSomente(string Status);
    private sealed record CriarLinha(Guid Id, string Descricao, decimal Quantidade, decimal Reserva);
    private sealed record CotacaoHeader(Guid Id, string Numero, int Rodada, Guid RequisicaoId, string NumeroRequisicao, string Status, DateTime Prazo, DateTime CriadoEm, DateTime? SelecionadoEm, long Version);
    private sealed record ConviteRaw(Guid Id, Guid FornecedorId, string FornecedorNome, string FornecedorDocumento, string Status, DateTime ExpiraEm, long Version, DateTime? Responder, int ItensRespondidos);
    private sealed record ComparativoHeader(Guid CotacaoId, string Numero, string Status, DateTime Prazo, long Version);
    private sealed record ComparativoLinhaRow(Guid RequisicaoItemId, int Ordem, string Descricao, string? Especificacao, string Unidade, decimal Quantidade);
    private sealed record OfertaRaw(Guid ConviteId, Guid FornecedorId, string FornecedorNome, Guid RequisicaoItemId, decimal PrecoUnitario, decimal Desconto, decimal Imposto, decimal Frete, int PrazoDias, string? Marca, string? Fabricante, bool Recusado);
    private sealed record ConviteLock(Guid FornecedorId, string Status, long Version, DateTime ExpiraEm);
    private sealed record CotacaoLock(Guid RequisicaoId, string Status, long Version, DateTime? SelecionadoEm);
    private sealed record SelecItem(Guid RiId, string Descricao, decimal Qtd, Guid Prod, bool Insp);
    private sealed record ConviteInfo(Guid Id, Guid FornecedorId, string FornecedorNome);
    private sealed record EscopoEscolha(SelecItem Item, Guid ConviteId, Guid FornecedorId, string FornecedorNome, OfertaRaw Oferta, decimal Custo, string? Justificativa);
    private sealed record PedidoHeaderRow(Guid Id, string Numero, string Status, Guid? FornecedorId, string? FornecedorNome, decimal ValorTotal, DateOnly? Previsao, DateTime CriadoEm, Guid? CotacaoId, string? NumeroCotacao, Guid? RequisicaoId, string? NumeroRequisicao, long Version);
}
