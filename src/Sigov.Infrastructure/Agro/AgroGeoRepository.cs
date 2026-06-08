using System.Text.Json;
using Dapper;
using Sigov.Application.Agro.Geo;
using Sigov.Application.Common;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro;

public sealed class AgroGeoRepository : IAgroGeoRepository
{
    private readonly DapperContext _context;

    public AgroGeoRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<AgroGeoCamadaResponse>> ListarCamadasAsync(long tenantId, long? entidadeId, AgroGeoFiltro filtro, CancellationToken cancellationToken)
    {
        const string countSql = "select count(*) from sigov.agro_geo_camada where tenant_id=@TenantId and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId) and (@Busca is null or nome ilike @Busca or codigo ilike @Busca);";
        const string sql = """
            select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_camada as TipoCamada, descricao as Descricao, publica as Publica, ativo as Ativo
              from sigov.agro_geo_camada
             where tenant_id=@TenantId and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId) and (@Busca is null or nome ilike @Busca or codigo ilike @Busca)
             order by nome asc limit @Limit offset @Offset;
            """;
        var parameters = Params(tenantId, entidadeId, filtro);
        using var connection = _context.CreateConnection();
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AgroGeoCamadaResponse>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new PagedResult<AgroGeoCamadaResponse>(rows.AsList(), filtro.Page, filtro.PageSize, total);
    }

    public async Task<AgroGeoCamadaResponse?> ObterCamadaAsync(long tenantId, long? entidadeId, long id, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_camada as TipoCamada, descricao as Descricao, publica as Publica, ativo as Ativo from sigov.agro_geo_camada where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AgroGeoCamadaResponse>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<long> CriarCamadaAsync(long tenantId, long? entidadeId, long? usuarioId, AgroGeoCamadaRequest request, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.agro_geo_camada(tenant_id,entidade_id,codigo,nome,tipo_camada,descricao,publica,estilo_json,ativo,created_by) values(@TenantId,@EntidadeId,@Codigo,@Nome,@TipoCamada,@Descricao,@Publica,cast(@EstiloJson as jsonb),@Ativo,@UsuarioId) returning id;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, request.Codigo, request.Nome, request.TipoCamada, request.Descricao, request.Publica, EstiloJson = request.EstiloJson ?? "{}", request.Ativo, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AtualizarCamadaAsync(long tenantId, long? entidadeId, long id, long? usuarioId, AgroGeoCamadaRequest request, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.agro_geo_camada set codigo=@Codigo,nome=@Nome,tipo_camada=@TipoCamada,descricao=@Descricao,publica=@Publica,estilo_json=cast(@EstiloJson as jsonb),ativo=@Ativo,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, request.Codigo, request.Nome, request.TipoCamada, request.Descricao, request.Publica, EstiloJson = request.EstiloJson ?? "{}", request.Ativo, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task ExcluirCamadaAsync(long tenantId, long? entidadeId, long id, long? usuarioId, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.agro_geo_camada set is_deleted=true,ativo=false,deleted_at=now(),deleted_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<PagedResult<AgroGeoFeicaoResponse>> ListarFeicoesAsync(long tenantId, long? entidadeId, AgroGeoFiltro filtro, CancellationToken cancellationToken)
    {
        const string countSql = "select count(*) from sigov.agro_geo_feicao where tenant_id=@TenantId and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId) and (@Busca is null or nome ilike @Busca);";
        const string sql = """
            select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, camada_id as CamadaId, nome as Nome, tipo_geometria as TipoGeometria, latitude as Latitude, longitude as Longitude, geojson::text as GeoJson, ativo as Ativo
              from sigov.agro_geo_feicao
             where tenant_id=@TenantId and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId) and (@Busca is null or nome ilike @Busca)
             order by id desc limit @Limit offset @Offset;
            """;
        var parameters = Params(tenantId, entidadeId, filtro);
        using var connection = _context.CreateConnection();
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AgroGeoFeicaoResponse>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new PagedResult<AgroGeoFeicaoResponse>(rows.AsList(), filtro.Page, filtro.PageSize, total);
    }

    public async Task<AgroGeoFeicaoResponse?> ObterFeicaoAsync(long tenantId, long? entidadeId, long id, CancellationToken cancellationToken)
    {
        const string sql = "select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, camada_id as CamadaId, nome as Nome, tipo_geometria as TipoGeometria, latitude as Latitude, longitude as Longitude, geojson::text as GeoJson, ativo as Ativo from sigov.agro_geo_feicao where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AgroGeoFeicaoResponse>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<long> CriarFeicaoAsync(long tenantId, long? entidadeId, long? usuarioId, AgroGeoFeicaoRequest request, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.agro_geo_feicao(tenant_id,entidade_id,camada_id,nome,tipo_geometria,latitude,longitude,geojson,propriedades_json,ativo,created_by) values(@TenantId,@EntidadeId,@CamadaId,@Nome,@TipoGeometria,@Latitude,@Longitude,cast(@GeoJson as jsonb),cast(@PropriedadesJson as jsonb),@Ativo,@UsuarioId) returning id;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, FeicaoParams(tenantId, entidadeId, usuarioId, request), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task AtualizarFeicaoAsync(long tenantId, long? entidadeId, long id, long? usuarioId, AgroGeoFeicaoRequest request, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.agro_geo_feicao set camada_id=@CamadaId,nome=@Nome,tipo_geometria=@TipoGeometria,latitude=@Latitude,longitude=@Longitude,geojson=cast(@GeoJson as jsonb),propriedades_json=cast(@PropriedadesJson as jsonb),ativo=@Ativo,updated_at=now(),updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, FeicaoParams(tenantId, entidadeId, usuarioId, request, id), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task ExcluirFeicaoAsync(long tenantId, long? entidadeId, long id, long? usuarioId, CancellationToken cancellationToken)
    {
        const string sql = "update sigov.agro_geo_feicao set is_deleted=true,ativo=false,deleted_at=now(),deleted_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> ExportarGeoJsonAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken)
    {
        const string sql = "select nome as Nome, tipo_geometria as TipoGeometria, latitude as Latitude, longitude as Longitude, geojson::text as GeoJson from sigov.agro_geo_feicao where tenant_id=@TenantId and is_deleted=false and ((@EntidadeId is null) or entidade_id=@EntidadeId) order by id;";
        using var connection = _context.CreateConnection();
        var rows = (await connection.QueryAsync<ExportRow>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId }, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();
        var features = rows.Select(ToFeature).ToArray();
        return JsonSerializer.Serialize(new { type = "FeatureCollection", features });
    }

    private static object Params(long tenantId, long? entidadeId, AgroGeoFiltro filtro) => new { TenantId = tenantId, EntidadeId = entidadeId, Busca = string.IsNullOrWhiteSpace(filtro.Busca) ? null : $"%{filtro.Busca}%", Limit = filtro.PageSize, Offset = (filtro.Page - 1) * filtro.PageSize };
    private static object FeicaoParams(long tenantId, long? entidadeId, long? usuarioId, AgroGeoFeicaoRequest request, long? id = null) => new { TenantId = tenantId, EntidadeId = entidadeId, Id = id, request.CamadaId, request.Nome, request.TipoGeometria, request.Latitude, request.Longitude, GeoJson = request.GeoJson, PropriedadesJson = request.PropriedadesJson ?? "{}", request.Ativo, UsuarioId = usuarioId };

    private static object ToFeature(ExportRow row)
    {
        object? geometry = null;
        if (!string.IsNullOrWhiteSpace(row.GeoJson))
        {
            geometry = JsonSerializer.Deserialize<JsonElement>(row.GeoJson);
        }
        else if (row.Latitude.HasValue && row.Longitude.HasValue)
        {
            geometry = new { type = "Point", coordinates = new[] { row.Longitude.Value, row.Latitude.Value } };
        }

        return new { type = "Feature", properties = new { nome = row.Nome, tipoGeometria = row.TipoGeometria }, geometry };
    }

    private sealed record ExportRow(string Nome, string TipoGeometria, decimal? Latitude, decimal? Longitude, string? GeoJson);
}
