using Dapper;
using Npgsql;
using Sigov.Application.Almoxarifado;

namespace Sigov.Infrastructure.Almoxarifado;

public sealed partial class AlmoxarifadoService
{
    public async Task<ReposicaoPainel> ListarReposicaoAsync(long tenantId, long entidadeId, ReposicaoFiltro filtro, CancellationToken ct)
    {
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 100);
        var horizonte = Math.Clamp(filtro.HorizonteDias, 1, 365);
        const string cte = """
with base as (
 select l.id almoxarifado_id,l.nome almoxarifado_nome,m.id material_id,m.codigo material_codigo,m.descricao material_descricao,m.unidade_medida,
        coalesce(s.quantidade,0) saldo_fisico,p.estoque_minimo,p.estoque_alvo,p.multiplo_compra,p.quantidade_minima_pedido,
        coalesce((select sum(greatest(ri.quantidade_solicitada-ri.quantidade_atendida,0)) from sigov.almoxarifado_requisicao r join sigov.almoxarifado_requisicao_item ri on ri.requisicao_id=r.id and ri.tenant_id=r.tenant_id and ri.entidade_id=r.entidade_id where r.tenant_id=@T and r.entidade_id=@E and r.almoxarifado_id=l.id and ri.material_id=m.id and r.status in ('ENVIADA','APROVADA')),0) compromissos,
        exists(select 1 from sigov.almoxarifado_transferencia tr join sigov.almoxarifado_transferencia_item ti on ti.transferencia_id=tr.id and ti.tenant_id=tr.tenant_id and ti.entidade_id=tr.entidade_id where tr.tenant_id=@T and tr.entidade_id=@E and tr.destino_id=l.id and ti.material_id=m.id and tr.status in ('EM_TRANSITO','RECEBIDA_PARCIAL','DIVERGENCIA')) em_transito,
        (p.id is null or not p.ativa or current_date<p.vigencia_inicio or (p.vigencia_fim is not null and current_date>p.vigencia_fim)) politica_incompleta
 from sigov.almoxarifado_estoque s
 join sigov.almoxarifado_local l on l.tenant_id=s.tenant_id and l.entidade_id=s.entidade_id and l.id=s.almoxarifado_id and l.ativo
 join sigov.almoxarifado_material m on m.tenant_id=s.tenant_id and m.entidade_id=s.entidade_id and m.id=s.material_id and m.ativo
 left join sigov.almoxarifado_politica_reposicao p on p.tenant_id=s.tenant_id and p.entidade_id=s.entidade_id and p.almoxarifado_id=s.almoxarifado_id and p.material_id=s.material_id
 where s.tenant_id=@T and s.entidade_id=@E and (@L is null or l.id=@L) and (@B is null or m.codigo ilike '%'||@B||'%' or m.descricao ilike '%'||@B||'%')
), classified as (
 select *,saldo_fisico-compromissos posicao,
 case when politica_incompleta then 'CONFIGURACAO_PENDENTE' when saldo_fisico-compromissos<estoque_minimo then 'ABAIXO_MINIMO' when em_transito then 'EM_ANDAMENTO' else 'REGULAR' end situacao
 from base)
""";
        var args = new { T = tenantId, E = entidadeId, L = filtro.AlmoxarifadoId, B = N(filtro.Busca), S = N(filtro.Situacao), Limit = tamanho, Offset = (pagina - 1) * tamanho };
        await using var connection = factory.CreateConnection();
        var summary = await connection.QuerySingleAsync<ResumoReposicao>(new CommandDefinition(cte + "select count(*) total,count(*) filter(where situacao='ABAIXO_MINIMO') abaixo_minimo,count(*) filter(where situacao='EM_ANDAMENTO') em_andamento,count(*) filter(where situacao='CONFIGURACAO_PENDENTE') incompletas from classified where (@S is null or situacao=@S)", args, cancellationToken: ct));
        var rows = (await connection.QueryAsync<LinhaReposicao>(new CommandDefinition(cte + "select * from classified where (@S is null or situacao=@S) order by case situacao when 'CONFIGURACAO_PENDENTE' then 0 when 'ABAIXO_MINIMO' then 1 when 'EM_ANDAMENTO' then 2 else 3 end,material_descricao,almoxarifado_nome,material_id limit @Limit offset @Offset", args, cancellationToken: ct))).AsList();
        var calculatedAt = DateTimeOffset.UtcNow;
        var items = rows.Select(x => Projetar(x, calculatedAt)).ToList();
        return new(new(items, pagina, tamanho, summary.Total), new(summary.Total, summary.AbaixoMinimo, summary.EmAndamento, summary.Incompletas), filtro with { HorizonteDias = horizonte, Pagina = pagina, TamanhoPagina = tamanho });
    }

    public async Task<PoliticaReposicaoDto?> ObterPoliticaReposicaoAsync(long tenantId, long entidadeId, long almoxarifadoId, long materialId, CancellationToken ct)
    {
        const string sql = "select p.id,p.almoxarifado_id AlmoxarifadoId,p.material_id MaterialId,p.estoque_minimo EstoqueMinimo,p.estoque_alvo EstoqueAlvo,p.multiplo_compra MultiploCompra,p.quantidade_minima_pedido QuantidadeMinimaPedido,p.prazo_reposicao_dias PrazoReposicaoDias,p.fornecedor_preferencial_id FornecedorPreferencialId,f.nome FornecedorNome,p.vigencia_inicio VigenciaInicio,p.vigencia_fim VigenciaFim,p.ativa,p.versao from sigov.almoxarifado_politica_reposicao p left join sigov.compras_fornecedor f on f.tenant_id=p.tenant_id and f.entidade_id=p.entidade_id and f.id=p.fornecedor_preferencial_id where p.tenant_id=@T and p.entidade_id=@E and p.almoxarifado_id=@L and p.material_id=@M";
        await using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PoliticaReposicaoDto>(new CommandDefinition(sql, new { T = tenantId, E = entidadeId, L = almoxarifadoId, M = materialId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FornecedorReposicaoDto>> ListarFornecedoresReposicaoAsync(long tenantId, long entidadeId, CancellationToken ct)
    {
        await using var connection = factory.CreateConnection();
        return (await connection.QueryAsync<FornecedorReposicaoDto>(new CommandDefinition("select id,nome from sigov.compras_fornecedor where tenant_id=@T and entidade_id=@E and status='ATIVO' order by nome,id limit 500", new { T = tenantId, E = entidadeId }, cancellationToken: ct))).AsList();
    }

    public async Task SalvarPoliticaReposicaoAsync(long tenantId, long entidadeId, long usuarioId, string correlationId, PoliticaReposicaoInput input, CancellationToken ct)
    {
        ValidarPolitica(input);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var contextOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.almoxarifado_local where tenant_id=@T and entidade_id=@E and id=@L and ativo) and exists(select 1 from sigov.almoxarifado_material where tenant_id=@T and entidade_id=@E and id=@M and ativo)", new { T = tenantId, E = entidadeId, L = input.AlmoxarifadoId, M = input.MaterialId }, tx, cancellationToken: ct));
        if (!contextOk) throw new InvalidOperationException("Material ou almoxarifado não pertence ao contexto ativo.");
        if (input.FornecedorPreferencialId is not null && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.compras_fornecedor where tenant_id=@T and entidade_id=@E and id=@F and status='ATIVO')", new { T = tenantId, E = entidadeId, F = input.FornecedorPreferencialId }, tx, cancellationToken: ct))) throw new InvalidOperationException("Fornecedor preferencial não pertence ao contexto ou não está ativo.");
        var before = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("select row_to_json(p)::text from sigov.almoxarifado_politica_reposicao p where tenant_id=@T and entidade_id=@E and almoxarifado_id=@L and material_id=@M for update", new { T = tenantId, E = entidadeId, L = input.AlmoxarifadoId, M = input.MaterialId }, tx, cancellationToken: ct));
        const string sql = "insert into sigov.almoxarifado_politica_reposicao(tenant_id,entidade_id,almoxarifado_id,material_id,estoque_minimo,estoque_alvo,multiplo_compra,quantidade_minima_pedido,prazo_reposicao_dias,fornecedor_preferencial_id,vigencia_inicio,vigencia_fim,ativa,versao,created_by,updated_by) values(@T,@E,@AlmoxarifadoId,@MaterialId,@EstoqueMinimo,@EstoqueAlvo,@MultiploCompra,@QuantidadeMinimaPedido,@PrazoReposicaoDias,@FornecedorPreferencialId,@VigenciaInicio,@VigenciaFim,@Ativa,1,@U,@U) on conflict(tenant_id,entidade_id,almoxarifado_id,material_id) do update set estoque_minimo=excluded.estoque_minimo,estoque_alvo=excluded.estoque_alvo,multiplo_compra=excluded.multiplo_compra,quantidade_minima_pedido=excluded.quantidade_minima_pedido,prazo_reposicao_dias=excluded.prazo_reposicao_dias,fornecedor_preferencial_id=excluded.fornecedor_preferencial_id,vigencia_inicio=excluded.vigencia_inicio,vigencia_fim=excluded.vigencia_fim,ativa=excluded.ativa,versao=sigov.almoxarifado_politica_reposicao.versao+1,updated_at=now(),updated_by=@U where sigov.almoxarifado_politica_reposicao.versao=@Versao returning id";
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { T = tenantId, E = entidadeId, U = usuarioId, input.AlmoxarifadoId, input.MaterialId, input.EstoqueMinimo, input.EstoqueAlvo, input.MultiploCompra, input.QuantidadeMinimaPedido, input.PrazoReposicaoDias, input.FornecedorPreferencialId, input.VigenciaInicio, input.VigenciaFim, input.Ativa, input.Versao }, tx, cancellationToken: ct));
        if (id is null) throw new InvalidOperationException("A política foi alterada por outro usuário. Recarregue os dados antes de salvar.");
        await Audit(connection, tx, tenantId, entidadeId, "POLITICA_REPOSICAO", id.Value, before is null ? "CRIAR" : "EDITAR", before, input, usuarioId, correlationId, ct);
        await tx.CommitAsync(ct);
    }

    private static void ValidarPolitica(PoliticaReposicaoInput input)
    {
        if (input.EstoqueMinimo < 0 || input.EstoqueAlvo < 0 || input.QuantidadeMinimaPedido < 0 || input.PrazoReposicaoDias < 0) throw new ArgumentException("Mínimo, alvo, quantidade mínima e prazo não podem ser negativos.");
        if (input.EstoqueAlvo < input.EstoqueMinimo) throw new ArgumentException("O estoque alvo deve ser maior ou igual ao estoque mínimo.");
        if (input.MultiploCompra is <= 0) throw new ArgumentException("O múltiplo de compra deve ser positivo quando informado.");
        if (input.VigenciaFim < input.VigenciaInicio) throw new ArgumentException("O fim da vigência não pode anteceder o início.");
    }

    private static ReposicaoItemDto Projetar(LinhaReposicao x, DateTimeOffset at)
    {
        if (x.PoliticaIncompleta) return new(x.AlmoxarifadoId,x.AlmoxarifadoNome,x.MaterialId,x.MaterialCodigo,x.MaterialDescricao,x.UnidadeMedida,x.SaldoFisico,x.Compromissos,0,x.Posicao,null,null,null,"CONFIGURACAO_PENDENTE","Saldo e demandas internas","Política ausente, inativa ou fora da vigência.","Análise incompleta: mínimo e alvo válidos são obrigatórios.",at);
        var necessidade = Math.Max(0, x.EstoqueAlvo!.Value - x.Posicao);
        var minima = x.QuantidadeMinimaPedido ?? 0;
        var baseCompra = Math.Max(necessidade, minima);
        var sugerida = x.MultiploCompra is > 0 ? Math.Ceiling(baseCompra / x.MultiploCompra.Value) * x.MultiploCompra.Value : baseCompra;
        var impedimento = x.EmTransito ? "Transferência em trânsito sem data confiável; não somada às entradas elegíveis." : null;
        var explicacao = $"{x.SaldoFisico:0.####} físico + 0 entradas elegíveis − {x.Compromissos:0.####} demandas ainda não atendidas = {x.Posicao:0.####}; alvo {x.EstoqueAlvo:0.####}; necessidade {necessidade:0.####}" + (x.QuantidadeMinimaPedido is > 0 ? $"; mínimo de pedido {x.QuantidadeMinimaPedido:0.####}" : "") + (x.MultiploCompra is > 0 ? $"; arredondada ao múltiplo {x.MultiploCompra:0.####}" : "") + ".";
        return new(x.AlmoxarifadoId,x.AlmoxarifadoNome,x.MaterialId,x.MaterialCodigo,x.MaterialDescricao,x.UnidadeMedida,x.SaldoFisico,x.Compromissos,0,x.Posicao,x.EstoqueMinimo,x.EstoqueAlvo,sugerida,x.Situacao,"Saldo e demandas internas",impedimento,explicacao,at);
    }

    private sealed record LinhaReposicao(long AlmoxarifadoId,string AlmoxarifadoNome,long MaterialId,string MaterialCodigo,string MaterialDescricao,string UnidadeMedida,decimal SaldoFisico,decimal Compromissos,decimal? EstoqueMinimo,decimal? EstoqueAlvo,decimal? MultiploCompra,decimal? QuantidadeMinimaPedido,bool EmTransito,bool PoliticaIncompleta,decimal Posicao,string Situacao);
    private sealed record ResumoReposicao(long Total,long AbaixoMinimo,long EmAndamento,long Incompletas);
}
