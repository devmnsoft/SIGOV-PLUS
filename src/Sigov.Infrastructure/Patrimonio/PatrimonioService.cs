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
