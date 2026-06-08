using System.Text.Json;
using Dapper;
using Sigov.Application.Agro.Comercial;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Agro.Repositories;

public sealed class AgroPainelComercialRepository : IAgroPainelComercialRepository
{
    private readonly DapperContext _context;
    public AgroPainelComercialRepository(DapperContext context) => _context = context;
    public async Task<AgroPainelComercialResponse> ObterAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken)
    {
        using var cn = _context.CreateConnection();
        var config = await cn.QuerySingleOrDefaultAsync(new CommandDefinition("select titulo, subtitulo from sigov.agro_painel_comercial_config where tenant_id=@TenantId and ((@EntidadeId is null and entidade_id is null) or entidade_id=@EntidadeId) and ativo=true order by updated_at desc nulls last, created_at desc limit 1", new { TenantId = tenantId, EntidadeId = entidadeId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return await BuildAsync(cn, tenantId, entidadeId, config?.titulo ?? "SIGOV Agro", config?.subtitulo ?? "Gestão rural integrada para municípios.", cancellationToken).ConfigureAwait(false);
    }
    public async Task<AgroPainelComercialResponse> AtualizarAsync(long tenantId, long? entidadeId, AgroPainelComercialConfigRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.agro_painel_comercial_config(tenant_id,entidade_id,titulo,subtitulo,mostrar_produtores,mostrar_producao,mostrar_pecuaria,mostrar_mapa,mostrar_programas,mostrar_estradas,mostrar_feiras,mostrar_agroindustrias,configuracao_json)
values(@TenantId,@EntidadeId,@Titulo,@Subtitulo,@MostrarProdutores,@MostrarProducao,@MostrarPecuaria,@MostrarMapa,@MostrarProgramas,@MostrarEstradas,@MostrarFeiras,@MostrarAgroindustrias,cast(@Config as jsonb))
on conflict (tenant_id, entidade_id) do update set titulo=excluded.titulo, subtitulo=excluded.subtitulo, mostrar_produtores=excluded.mostrar_produtores, mostrar_producao=excluded.mostrar_producao, mostrar_pecuaria=excluded.mostrar_pecuaria, mostrar_mapa=excluded.mostrar_mapa, mostrar_programas=excluded.mostrar_programas, mostrar_estradas=excluded.mostrar_estradas, mostrar_feiras=excluded.mostrar_feiras, mostrar_agroindustrias=excluded.mostrar_agroindustrias, configuracao_json=excluded.configuracao_json, updated_at=now();
";
        using var cn = _context.CreateConnection(); await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, EntidadeId = entidadeId, request.Titulo, request.Subtitulo, request.MostrarProdutores, request.MostrarProducao, request.MostrarPecuaria, request.MostrarMapa, request.MostrarProgramas, request.MostrarEstradas, request.MostrarFeiras, request.MostrarAgroindustrias, Config = JsonSerializer.Serialize(request) }, cancellationToken: cancellationToken)).ConfigureAwait(false); await cn.ExecuteAsync(new CommandDefinition("insert into sigov.agro_evento(tenant_id,entidade_id,tipo_evento,origem,payload_json,correlation_id) values(@TenantId,@EntidadeId,'AgroPainelComercialAtualizado','agro_painel_comercial_config',cast(@Payload as jsonb),@CorrelationId)", new { TenantId = tenantId, EntidadeId = entidadeId, Payload = JsonSerializer.Serialize(new { request.Titulo }), CorrelationId = Guid.NewGuid() }, cancellationToken: cancellationToken)).ConfigureAwait(false); return await BuildAsync(cn, tenantId, entidadeId, request.Titulo, request.Subtitulo, cancellationToken).ConfigureAwait(false);
    }
    public async Task<AgroPainelComercialResponse?> ObterPublicoAsync(string tenantSlug, CancellationToken cancellationToken)
    {
        using var cn = _context.CreateConnection(); var tenant = await cn.QuerySingleOrDefaultAsync(new CommandDefinition("select id, slug from sigov.tenant where slug=@Slug and ativo=true and is_deleted=false", new { Slug = tenantSlug }, cancellationToken: cancellationToken)).ConfigureAwait(false); if (tenant is null) return null; return await ObterAsync((long)tenant.id, null, cancellationToken).ConfigureAwait(false);
    }
    private static async Task<AgroPainelComercialResponse> BuildAsync(System.Data.IDbConnection cn, long tenantId, long? entidadeId, string titulo, string? subtitulo, CancellationToken ct)
    {
        var indicadores = await cn.QuerySingleOrDefaultAsync(new CommandDefinition("select total_produtores, total_propriedades, area_produtiva, producao_realizada, feiras_ativas, agroindustrias_ativas from sigov.vw_agro_bi_resumo where tenant_id=@TenantId and ((@EntidadeId is null and entidade_id is null) or entidade_id=@EntidadeId) limit 1", new { TenantId = tenantId, EntidadeId = entidadeId }, cancellationToken: ct)).ConfigureAwait(false);
        var dict = indicadores is null ? new Dictionary<string, decimal>() : ((IDictionary<string, object>)indicadores).ToDictionary(k => k.Key, k => Convert.ToDecimal(k.Value ?? 0));
        var funcionalidades = new[] { "produtores", "propriedades", "produção", "pecuária", "assistência técnica", "programas", "patrulha mecanizada", "estradas", "feiras", "agroindústrias", "dados abertos" };
        var beneficios = new[] { "gestão rural integrada", "apoio ao pequeno produtor", "controle territorial", "transparência", "planejamento de máquinas", "agricultura familiar" };
        return new AgroPainelComercialResponse(tenantId, entidadeId, titulo, subtitulo, beneficios, funcionalidades, dict, null, null);
    }
}
