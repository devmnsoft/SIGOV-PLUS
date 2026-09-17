using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Patrimonio;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Patrimonio;

public sealed class PatrimonioService(NpgsqlConnectionFactory factory) : IPatrimonioService
{
    public async Task<PatrimonioPagina<PatrimonioBemDto>> ListarBensAsync(long tenantId, PatrimonioBemFiltro filtro, CancellationToken ct)
    {
        var pagina = Math.Max(1, filtro.Pagina); var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 100);
        const string where = "b.tenant_id=@TenantId and not b.is_deleted and (@Busca is null or b.codigo_tombo ilike '%'||@Busca||'%' or b.descricao ilike '%'||@Busca||'%') and (@CategoriaId is null or b.categoria_id=@CategoriaId) and (@Situacao is null or b.situacao=@Situacao) and (@UnidadeId is null or b.unidade_id=@UnidadeId) and (@ResponsavelUsuarioId is null or b.responsavel_usuario_id=@ResponsavelUsuarioId)";
        var args = new { TenantId=tenantId, Busca=VazioNulo(filtro.Busca), filtro.CategoriaId, Situacao=VazioNulo(filtro.Situacao), filtro.UnidadeId, filtro.ResponsavelUsuarioId, Limit=tamanho, Offset=(pagina-1)*tamanho };
        await using var c = factory.CreateConnection();
        var total = await c.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from sigov.patrimonio_bem b where {where}", args, cancellationToken:ct));
        var items = (await c.QueryAsync<PatrimonioBemDto>(new CommandDefinition($"""select b.id,b.codigo_tombo CodigoTombo,b.descricao,b.categoria_id CategoriaId,c.nome Categoria,b.tipo_bem TipoBem,b.marca,b.modelo,b.numero_serie NumeroSerie,b.data_aquisicao DataAquisicao,b.valor_aquisicao ValorAquisicao,b.valor_atual ValorAtual,b.estado_conservacao EstadoConservacao,b.situacao,b.unidade_id UnidadeId,b.setor_id SetorId,b.responsavel_usuario_id ResponsavelUsuarioId,b.localizacao,b.observacao,b.created_at CreatedAt,b.updated_at UpdatedAt,uo.nome UnidadeNome,coalesce(p.nome,u.login) ResponsavelNome from sigov.patrimonio_bem b left join sigov.patrimonio_categoria c on c.id=b.categoria_id and c.tenant_id=b.tenant_id left join sigov.unidade_organizacional uo on uo.id=b.unidade_id left join sigov.usuario u on u.id=b.responsavel_usuario_id left join sigov.pessoa p on p.id=u.pessoa_id where {where} order by b.codigo_tombo,b.id limit @Limit offset @Offset""",args,cancellationToken:ct))).AsList();
        return new(items,pagina,tamanho,total);
    }

    public async Task<PatrimonioBemDto?> ObterBemAsync(long tenantId,long id,CancellationToken ct)
    { await using var c=factory.CreateConnection(); return await c.QuerySingleOrDefaultAsync<PatrimonioBemDto>(new CommandDefinition("select b.id,b.codigo_tombo CodigoTombo,b.descricao,b.categoria_id CategoriaId,c.nome Categoria,b.tipo_bem TipoBem,b.marca,b.modelo,b.numero_serie NumeroSerie,b.data_aquisicao DataAquisicao,b.valor_aquisicao ValorAquisicao,b.valor_atual ValorAtual,b.estado_conservacao EstadoConservacao,b.situacao,b.unidade_id UnidadeId,b.setor_id SetorId,b.responsavel_usuario_id ResponsavelUsuarioId,b.localizacao,b.observacao,b.created_at CreatedAt,b.updated_at UpdatedAt,uo.nome UnidadeNome,coalesce(p.nome,u.login) ResponsavelNome from sigov.patrimonio_bem b left join sigov.patrimonio_categoria c on c.id=b.categoria_id and c.tenant_id=b.tenant_id left join sigov.unidade_organizacional uo on uo.id=b.unidade_id left join sigov.usuario u on u.id=b.responsavel_usuario_id left join sigov.pessoa p on p.id=u.pessoa_id where b.tenant_id=@TenantId and b.id=@Id and not b.is_deleted",new{TenantId=tenantId,Id=id},cancellationToken:ct)); }

    public async Task<PatrimonioBemDetalhe?> ObterBemDetalheAsync(long tenantId,long id,CancellationToken ct)
    {
        var bem=await ObterBemAsync(tenantId,id,ct);
        if(bem is null)return null;
        await using var c=factory.CreateConnection();
        var movimentos=(await c.QueryAsync<PatrimonioMovimentacaoDto>(new CommandDefinition("select id,bem_id BemId,unidade_origem_id UnidadeOrigemId,unidade_destino_id UnidadeDestinoId,responsavel_origem_id ResponsavelOrigemId,responsavel_destino_id ResponsavelDestinoId,localizacao_origem LocalizacaoOrigem,localizacao_destino LocalizacaoDestino,tipo_movimentacao TipoMovimentacao,justificativa,data_movimentacao DataMovimentacao,usuario_id UsuarioId from sigov.patrimonio_movimentacao where tenant_id=@TenantId and bem_id=@Id order by data_movimentacao desc,id desc",new{TenantId=tenantId,Id=id},cancellationToken:ct))).AsList();
        var termos=(await c.QueryAsync<PatrimonioTermoDto>(new CommandDefinition("select t.id,t.bem_id BemId,t.responsavel_proposto_id ResponsavelPropostoId,coalesce(p.nome,u.login) ResponsavelProposto,t.status,t.conteudo_snapshot::text ConteudoSnapshot,t.motivo_recusa MotivoRecusa,t.proposto_em PropostoEm,t.decidido_em DecididoEm,t.vigencia_inicio VigenciaInicio,t.vigencia_fim VigenciaFim from sigov.patrimonio_termo_responsabilidade t join sigov.usuario u on u.id=t.responsavel_proposto_id left join sigov.pessoa p on p.id=u.pessoa_id where t.tenant_id=@TenantId and t.bem_id=@Id order by t.proposto_em desc,t.id desc",new{TenantId=tenantId,Id=id},cancellationToken:ct))).AsList();
        var transferencias=(await c.QueryAsync<PatrimonioTransferenciaDto>(new CommandDefinition("select t.id,t.bem_id BemId,t.unidade_origem_id UnidadeOrigemId,uo.nome UnidadeOrigem,t.unidade_destino_id UnidadeDestinoId,ud.nome UnidadeDestino,t.localizacao_origem LocalizacaoOrigem,t.localizacao_destino LocalizacaoDestino,t.responsavel_destino_id ResponsavelDestinoId,t.status,t.justificativa,t.motivo_recusa MotivoRecusa,t.solicitada_em SolicitadaEm,t.expedida_em ExpedidaEm,t.recebida_em RecebidaEm,t.versao from sigov.patrimonio_transferencia t left join sigov.unidade_organizacional uo on uo.id=t.unidade_origem_id join sigov.unidade_organizacional ud on ud.id=t.unidade_destino_id where t.tenant_id=@TenantId and t.bem_id=@Id order by t.solicitada_em desc,t.id desc",new{TenantId=tenantId,Id=id},cancellationToken:ct))).AsList();
        var manutencoes=(await c.QueryAsync<PatrimonioManutencaoDto>(new CommandDefinition("select id,status,prioridade,descricao,created_at CreatedAt from sigov.manutencao_ordem_servico where tenant_id=@TenantId and alvo_tipo='BEM' and alvo_id=@Id order by created_at desc,id desc",new{TenantId=tenantId,Id=id},cancellationToken:ct))).AsList();
        var documento=await c.ExecuteScalarAsync<string?>(new CommandDefinition("select r.documento from sigov.patrimonio_bem b join sigov.compras_recebimento_item ri on ri.id=b.recebimento_item_id and ri.tenant_id=b.tenant_id join sigov.compras_recebimento r on r.id=ri.recebimento_id and r.tenant_id=ri.tenant_id where b.tenant_id=@TenantId and b.id=@Id",new{TenantId=tenantId,Id=id},cancellationToken:ct));
        return new(bem,movimentos,termos,transferencias,manutencoes,documento);
    }

    public async Task<long> CriarBemAsync(long tenantId,long usuarioId,string correlationId,PatrimonioBemInput input,CancellationToken ct)
    {
        ValidarBem(input); await using var c=factory.CreateConnection(); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        try { const string sql="""insert into sigov.patrimonio_bem(tenant_id,codigo_tombo,codigo_anterior,descricao,categoria_id,tipo_bem,marca,modelo,numero_serie,data_aquisicao,valor_aquisicao,valor_atual,estado_conservacao,situacao,unidade_id,setor_id,responsavel_usuario_id,localizacao,observacao,created_by,updated_by) values(@TenantId,@CodigoTombo,@CodigoAnterior,@Descricao,@CategoriaId,@TipoBem,@Marca,@Modelo,@NumeroSerie,@DataAquisicao,@ValorAquisicao,@ValorAtual,@EstadoConservacao,'ATIVO',@UnidadeId,@SetorId,@ResponsavelUsuarioId,@Localizacao,@Observacao,@UsuarioId,@UsuarioId) returning id""";
            var id=await c.ExecuteScalarAsync<long>(new CommandDefinition(sql,Args(input,tenantId,usuarioId),tx,cancellationToken:ct)); await Auditar(c,tx,tenantId,"patrimonio_bem",id,"CRIAR",null,input,usuarioId,correlationId,ct); await tx.CommitAsync(ct); return id; }
        catch(PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation){await tx.RollbackAsync(ct);throw new InvalidOperationException("O código de tombo já existe neste tenant.",e);} catch{await tx.RollbackAsync(ct);throw;}
    }

    public async Task EditarBemAsync(long tenantId,long usuarioId,string correlationId,long id,PatrimonioBemInput input,CancellationToken ct)
    {
        ValidarBem(input); await using var c=factory.CreateConnection(); await c.OpenAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
        try { var antes=await ObterJson(c,tx,"patrimonio_bem",tenantId,id,ct); if(antes is null) throw new KeyNotFoundException("Bem não encontrado.");
            const string sql="""update sigov.patrimonio_bem set codigo_tombo=@CodigoTombo,codigo_anterior=@CodigoAnterior,descricao=@Descricao,categoria_id=@CategoriaId,tipo_bem=@TipoBem,marca=@Marca,modelo=@Modelo,numero_serie=@NumeroSerie,data_aquisicao=@DataAquisicao,valor_aquisicao=@ValorAquisicao,valor_atual=@ValorAtual,estado_conservacao=@EstadoConservacao,unidade_id=@UnidadeId,setor_id=@SetorId,responsavel_usuario_id=@ResponsavelUsuarioId,localizacao=@Localizacao,observacao=@Observacao,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and not is_deleted""";
            var parameters=new DynamicParameters(Args(input,tenantId,usuarioId)); parameters.Add("Id",id); await c.ExecuteAsync(new CommandDefinition(sql,parameters,tx,cancellationToken:ct)); await Auditar(c,tx,tenantId,"patrimonio_bem",id,"EDITAR",antes,input,usuarioId,correlationId,ct); await tx.CommitAsync(ct); }
        catch(PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation){await tx.RollbackAsync(ct);throw new InvalidOperationException("O código de tombo já existe neste tenant.",e);} catch{await tx.RollbackAsync(ct);throw;}
    }
}