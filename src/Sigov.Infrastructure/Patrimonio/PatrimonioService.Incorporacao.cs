using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Patrimonio;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Patrimonio;

public sealed partial class PatrimonioService
{
    public async Task<IReadOnlyList<PatrimonioRecebimentoElegivelDto>> ListarRecebimentosElegiveisAsync(long tenantId,long entidadeId,CancellationToken ct)
    {
        const string sql="""select ri.id ItemId,r.documento,coalesce(f.nome,'Fornecedor não identificado') Fornecedor,pi.descricao Produto,ri.quantidade QuantidadeRecebida,ri.quantidade QuantidadeAceita,count(b.id) QuantidadeIncorporada,ri.quantidade-count(b.id) SaldoElegivel,pi.unidade,null::text NumeroSerie from sigov.compras_recebimento_item ri join sigov.compras_recebimento r on r.id=ri.recebimento_id and r.tenant_id=ri.tenant_id and r.entidade_id=ri.entidade_id join sigov.compras_processo_item pi on pi.id=ri.processo_item_id and pi.tenant_id=ri.tenant_id left join sigov.compras_contrato co on co.id=r.contrato_id left join sigov.compras_ata_registro_preco a on a.id=r.ata_id left join sigov.compras_fornecedor f on f.id=co.fornecedor_id or f.id=a.fornecedor_id left join sigov.patrimonio_bem b on b.recebimento_item_id=ri.id and b.tenant_id=ri.tenant_id and not b.is_deleted where ri.tenant_id=@Tenant and ri.entidade_id=@Entidade and r.status='INTEGRADO' and ri.tipo_material='PERMANENTE' group by ri.id,r.documento,f.nome,pi.descricao,ri.quantidade,pi.unidade having ri.quantidade-count(b.id)>0 order by r.documento,pi.descricao""";
        await using var c=factory.CreateConnection();return(await c.QueryAsync<PatrimonioRecebimentoElegivelDto>(new CommandDefinition(sql,new{Tenant=tenantId,Entidade=entidadeId},cancellationToken:ct))).AsList();
    }

    public async Task<IReadOnlyList<long>> IncorporarAsync(long tenantId,long entidadeId,long usuarioId,PatrimonioIncorporacaoInput input,CancellationToken ct)
    {
        if(input.Quantidade<=0||input.CategoriaId<=0||input.UnidadeId<=0||string.IsNullOrWhiteSpace(input.Localizacao))throw new ArgumentException("Quantidade, categoria, unidade e localização são obrigatórias.");
        if(string.IsNullOrWhiteSpace(input.CorrelationId)||input.CorrelationId.Length>100)throw new ArgumentException("Chave da operação inválida.");
        await using var c=factory.CreateConnection();await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        var repetida=(await c.QueryAsync<long>(new CommandDefinition("select b.id from sigov.patrimonio_incorporacao i join sigov.patrimonio_bem b on b.tenant_id=i.tenant_id and b.incorporacao_id=i.id where i.tenant_id=@Tenant and i.correlation_id=@Correlation order by b.id",new{Tenant=tenantId,Correlation=input.CorrelationId},tx,cancellationToken:ct))).AsList();
        if(repetida.Count>0){await tx.CommitAsync(ct);return repetida;}
        var origem=await c.QuerySingleOrDefaultAsync<OrigemIncorporacao>(new CommandDefinition("select ri.quantidade,ri.valor_unitario ValorUnitario,pi.descricao,pi.unidade,r.data_recebimento DataRecebimento from sigov.compras_recebimento_item ri join sigov.compras_recebimento r on r.id=ri.recebimento_id and r.tenant_id=ri.tenant_id join sigov.compras_processo_item pi on pi.id=ri.processo_item_id where ri.id=@Item and ri.tenant_id=@Tenant and ri.entidade_id=@Entidade and ri.tipo_material='PERMANENTE' and r.status='INTEGRADO' for update of ri",new{Item=input.RecebimentoItemId,Tenant=tenantId,Entidade=entidadeId},tx,cancellationToken:ct))??throw new InvalidOperationException("O item não está aceito e elegível para incorporação.");
        if(origem.Quantidade!=decimal.Truncate(origem.Quantidade))throw new InvalidOperationException("A unidade recebida não permite individualização implícita; registre composição ou conversão explícita.");
        var incorporada=await c.ExecuteScalarAsync<long>(new CommandDefinition("select count(*) from sigov.patrimonio_bem where tenant_id=@Tenant and recebimento_item_id=@Item and not is_deleted",new{Tenant=tenantId,Item=input.RecebimentoItemId},tx,cancellationToken:ct));
        if(input.Quantidade>origem.Quantidade-incorporada)throw new InvalidOperationException("Quantidade solicitada excede o saldo elegível atualizado.");
        var categoriaValida=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.patrimonio_categoria where tenant_id=@Tenant and id=@Categoria and ativo and not is_deleted)",new{Tenant=tenantId,Categoria=input.CategoriaId},tx,cancellationToken:ct));
        var unidadeValida=await c.ExecuteScalarAsync<bool>(new CommandDefinition("select exists(select 1 from sigov.unidade_organizacional where entidade_id=@Entidade and id=@Unidade and ativo and not is_deleted)",new{Entidade=entidadeId,Unidade=input.UnidadeId},tx,cancellationToken:ct));
        if(!categoriaValida||!unidadeValida)throw new InvalidOperationException("Categoria ou unidade não pertence ao contexto autorizado.");
        var incorporacaoId=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.patrimonio_incorporacao(tenant_id,entidade_id,recebimento_item_id,quantidade,correlation_id,usuario_id) values(@Tenant,@Entidade,@Item,@Quantidade,@Correlation,@Usuario) returning id",new{Tenant=tenantId,Entidade=entidadeId,Item=input.RecebimentoItemId,input.Quantidade,Correlation=input.CorrelationId,Usuario=usuarioId},tx,cancellationToken:ct));
        var ids=new List<long>(input.Quantidade);
        for(var n=0;n<input.Quantidade;n++)
        {
            var tombo=$"PAT-{DateTime.UtcNow:yyyy}-{incorporacaoId:D6}-{n+1:D3}";
            var id=await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.patrimonio_bem(tenant_id,codigo_tombo,descricao,categoria_id,tipo_bem,marca,modelo,numero_serie,data_aquisicao,valor_aquisicao,estado_conservacao,situacao,unidade_id,localizacao,observacao,recebimento_item_id,incorporacao_id,identificacao_serial_origem,created_by,updated_by) values(@Tenant,@Tombo,@Descricao,@Categoria,@Tipo,@Marca,@Modelo,@Serie,@Data,@Valor,@Estado,'ATIVO',@Unidade,@Localizacao,@Observacao,@Item,@Incorporacao,@Serie,@Usuario,@Usuario) returning id",new{Tenant=tenantId,Tombo=tombo,origem.Descricao,Categoria=input.CategoriaId,Tipo=input.TipoBem,Marca=VazioNulo(input.Marca),Modelo=VazioNulo(input.Modelo),Serie=(string?)null,Data=origem.DataRecebimento,Valor=origem.ValorUnitario,Estado=input.EstadoConservacao,Unidade=input.UnidadeId,Localizacao=input.Localizacao.Trim(),Observacao=VazioNulo(input.Observacao),Item=input.RecebimentoItemId,Incorporacao=incorporacaoId,Usuario=usuarioId},tx,cancellationToken:ct));
            ids.Add(id);await Auditar(c,tx,tenantId,"patrimonio_bem",id,"INCORPORAR",null,new{input.RecebimentoItemId,incorporacaoId,tombo},usuarioId,input.CorrelationId,ct);
        }
        await tx.CommitAsync(ct);return ids;
    }

    public async Task<PatrimonioCadastroOpcoes> ObterOpcoesAsync(long tenantId,long entidadeId,CancellationToken ct)
    { await using var c=factory.CreateConnection();var categorias=(await c.QueryAsync<PatrimonioOpcaoDto>(new CommandDefinition("select id,nome from sigov.patrimonio_categoria where tenant_id=@Tenant and ativo and not is_deleted order by nome",new{Tenant=tenantId},cancellationToken:ct))).AsList();var unidades=(await c.QueryAsync<PatrimonioOpcaoDto>(new CommandDefinition("select id,nome from sigov.unidade_organizacional where entidade_id=@Entidade and ativo and not is_deleted order by nome",new{Entidade=entidadeId},cancellationToken:ct))).AsList();var responsaveis=(await c.QueryAsync<PatrimonioOpcaoDto>(new CommandDefinition("select u.id,coalesce(p.nome,u.login) nome from sigov.usuario u left join sigov.pessoa p on p.id=u.pessoa_id where u.entidade_id=@Entidade and u.ativo and not u.is_deleted order by 2",new{Entidade=entidadeId},cancellationToken:ct))).AsList();return new(categorias,unidades,responsaveis); }
}
