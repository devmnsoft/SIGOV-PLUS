using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Agro.Transparencia;
using Sigov.Infrastructure.Agro.Sql;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro.Repositories;

public sealed class AgroTransparenciaRepository : IAgroTransparenciaRepository
{
    private readonly DapperContext _context;
    public AgroTransparenciaRepository(DapperContext context) => _context = context;
    public async Task<IReadOnlyCollection<AgroDatasetPublicoResponse>> ListarDatasetsAsync(long tenantId, long? entidadeId, bool somentePublicos, int page, int pageSize, CancellationToken cancellationToken) { using var cn = _context.CreateConnection(); var items = await cn.QueryAsync<AgroDatasetPublicoResponse>(new CommandDefinition(AgroTransparenciaSql.ListarDatasets, new { TenantId = tenantId, EntidadeId = entidadeId, SomentePublicos = somentePublicos, PageSize = pageSize, Offset = (page - 1) * pageSize }, cancellationToken: cancellationToken)).ConfigureAwait(false); return items.ToArray(); }
    public async Task<AgroDatasetPublicoResponse> CriarDatasetAsync(long tenantId, long? entidadeId, long usuarioId, AgroDatasetPublicoCreateRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.agro_dataset_publico(tenant_id,entidade_id,codigo,nome,descricao,tipo_dataset,formato_padrao,anonimizado,publico,created_by)
values(@TenantId,@EntidadeId,@Codigo,@Nome,@Descricao,@TipoDataset,@FormatoPadrao,@Anonimizado,@Publico,@UsuarioId)
on conflict (tenant_id, entidade_id, codigo) do update set nome=excluded.nome, descricao=excluded.descricao, tipo_dataset=excluded.tipo_dataset, formato_padrao=excluded.formato_padrao, anonimizado=excluded.anonimizado, publico=excluded.publico, updated_at=now(), updated_by=@UsuarioId
returning id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_dataset as TipoDataset, formato_padrao as FormatoPadrao, anonimizado as Anonimizado, publico as Publico, ativo as Ativo, ultima_publicacao_at as UltimaPublicacaoAt;
";
        using var cn = _context.CreateConnection(); var result = await cn.QuerySingleAsync<AgroDatasetPublicoResponse>(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, request.Codigo, request.Nome, request.Descricao, request.TipoDataset, request.FormatoPadrao, request.Anonimizado, Publico = request.Publico && request.Anonimizado, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false); await EventoAsync(cn, tenantId, entidadeId, "AgroDatasetCriado", "agro_dataset_publico", result.Id, usuarioId, new { request.Codigo }, cancellationToken).ConfigureAwait(false); return result;
    }
    public async Task<AgroDatasetPublicacaoResponse> PublicarAsync(long tenantId, long datasetId, long usuarioId, PublicarAgroDatasetRequest request, CancellationToken cancellationToken)
    {
        using var cn = _context.CreateConnection(); var dataset = await cn.QuerySingleAsync<AgroDatasetPublicoResponse>(new CommandDefinition("select id as Id, tenant_id as TenantId, entidade_id as EntidadeId, codigo as Codigo, nome as Nome, tipo_dataset as TipoDataset, formato_padrao as FormatoPadrao, anonimizado as Anonimizado, publico as Publico, ativo as Ativo, ultima_publicacao_at as UltimaPublicacaoAt from sigov.agro_dataset_publico where tenant_id=@TenantId and id=@Id and is_deleted=false", new { TenantId = tenantId, Id = datasetId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (!dataset.Anonimizado) throw new InvalidOperationException("Dataset não anonimizado não pode ser publicado.");
        var content = BuildDatasetContent(dataset, request.Formato); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        const string sql = @"insert into sigov.agro_dataset_publicacao(tenant_id,dataset_id,status,formato,conteudo_texto,hash_sha256,total_registros,publicado_at,publicado_by)
values(@TenantId,@DatasetId,'PUBLICADO',@Formato,@Conteudo,@Hash,1,now(),@UsuarioId)
returning id as Id, tenant_id as TenantId, dataset_id as DatasetId, status as Status, formato as Formato, conteudo_texto as ConteudoTexto, total_registros as TotalRegistros, publicado_at as PublicadoAt;
";
        var pub = await cn.QuerySingleAsync<AgroDatasetPublicacaoResponse>(new CommandDefinition(sql, new { TenantId = tenantId, DatasetId = datasetId, Formato = request.Formato.ToUpperInvariant(), Conteudo = content, Hash = hash, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await cn.ExecuteAsync(new CommandDefinition("update sigov.agro_dataset_publico set publico=true, ultima_publicacao_at=now(), updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@DatasetId", new { TenantId = tenantId, DatasetId = datasetId, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await EventoAsync(cn, tenantId, dataset.EntidadeId, "AgroDatasetPublicado", "agro_dataset_publico", datasetId, usuarioId, new { dataset.Codigo, request.Formato }, cancellationToken).ConfigureAwait(false); return pub;
    }
    public async Task<AgroDatasetPublicacaoResponse> SuspenderAsync(long tenantId, long datasetId, long usuarioId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.agro_dataset_publicacao(tenant_id,dataset_id,status,formato,created_at) values(@TenantId,@DatasetId,'SUSPENSO','CSV',now()) returning id as Id, tenant_id as TenantId, dataset_id as DatasetId, status as Status, formato as Formato, conteudo_texto as ConteudoTexto, total_registros as TotalRegistros, publicado_at as PublicadoAt;";
        using var cn = _context.CreateConnection(); var pub = await cn.QuerySingleAsync<AgroDatasetPublicacaoResponse>(new CommandDefinition(sql, new { TenantId = tenantId, DatasetId = datasetId }, cancellationToken: cancellationToken)).ConfigureAwait(false); await cn.ExecuteAsync(new CommandDefinition("update sigov.agro_dataset_publico set publico=false, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@DatasetId", new { TenantId = tenantId, DatasetId = datasetId, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false); await EventoAsync(cn, tenantId, null, "AgroDatasetSuspenso", "agro_dataset_publico", datasetId, usuarioId, new { datasetId }, cancellationToken).ConfigureAwait(false); return pub;
    }
    public async Task<long?> ResolverTenantPorSlugAsync(string tenantSlug, CancellationToken cancellationToken) { using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long?>(new CommandDefinition("select id from sigov.tenant where slug=@Slug and ativo=true and is_deleted=false", new { Slug = tenantSlug }, cancellationToken: cancellationToken)).ConfigureAwait(false); }
    public async Task<AgroDatasetPublicacaoResponse?> ObterPublicacaoAsync(long tenantId, string codigo, string formato, CancellationToken cancellationToken)
    {
        const string sql = @"select p.id as Id, p.tenant_id as TenantId, p.dataset_id as DatasetId, p.status as Status, p.formato as Formato, p.conteudo_texto as ConteudoTexto, p.total_registros as TotalRegistros, p.publicado_at as PublicadoAt
  from sigov.agro_dataset_publicacao p join sigov.agro_dataset_publico d on d.id=p.dataset_id and d.tenant_id=p.tenant_id
 where p.tenant_id=@TenantId and d.codigo=@Codigo and d.publico=true and d.anonimizado=true and p.status='PUBLICADO' and upper(p.formato)=upper(@Formato)
 order by p.publicado_at desc nulls last, p.created_at desc limit 1;
";
        using var cn = _context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<AgroDatasetPublicacaoResponse>(new CommandDefinition(sql, new { TenantId = tenantId, Codigo = codigo, Formato = formato }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
    public async Task RegistrarDownloadAsync(long tenantId, long? datasetId, long? publicacaoId, string formato, string? ip, string? userAgent, CancellationToken cancellationToken) { using var cn = _context.CreateConnection(); await cn.ExecuteAsync(new CommandDefinition("insert into sigov.agro_dataset_download_log(tenant_id,dataset_id,publicacao_id,formato,ip,user_agent,metadados) values(@TenantId,@DatasetId,@PublicacaoId,@Formato,@Ip,@UserAgent,'{}'::jsonb)", new { TenantId = tenantId, DatasetId = datasetId, PublicacaoId = publicacaoId, Formato = formato, Ip = ip, UserAgent = userAgent }, cancellationToken: cancellationToken)).ConfigureAwait(false); await EventoAsync(cn, tenantId, null, "AgroDatasetBaixado", "agro_dataset_publicacao", publicacaoId, null, new { datasetId, formato }, cancellationToken).ConfigureAwait(false); }
    private static string BuildDatasetContent(AgroDatasetPublicoResponse dataset, string formato) => formato.Equals("JSON", StringComparison.OrdinalIgnoreCase) ? JsonSerializer.Serialize(new[] { new { dataset.Codigo, dataset.Nome, dataset.TipoDataset, anonimizado = true } }) : formato.Equals("GEOJSON", StringComparison.OrdinalIgnoreCase) ? JsonSerializer.Serialize(new { type = "FeatureCollection", features = Array.Empty<object>() }) : "codigo;nome;tipo_dataset;anonimizado\n" + $"{dataset.Codigo};{dataset.Nome};{dataset.TipoDataset};true\n";
    private static Task EventoAsync(System.Data.IDbConnection cn, long tenantId, long? entidadeId, string tipo, string origem, long? origemId, long? usuarioId, object payload, CancellationToken ct) => cn.ExecuteAsync(new CommandDefinition("insert into sigov.agro_evento(tenant_id,entidade_id,tipo_evento,origem,origem_id,payload_json,created_by,correlation_id) values(@TenantId,@EntidadeId,@Tipo,@Origem,@OrigemId,cast(@Payload as jsonb),@UsuarioId,@CorrelationId)", new { TenantId = tenantId, EntidadeId = entidadeId, Tipo = tipo, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), UsuarioId = usuarioId, CorrelationId = Guid.NewGuid() }, cancellationToken: ct));
}
