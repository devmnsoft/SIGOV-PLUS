using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Rh;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Rh;

public sealed class RhRepository : BaseRepository, IRhRepository
{
    private readonly DapperContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> Tabelas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["servidores"] = "sigov.servidor", ["cargos"] = "sigov.cargo", ["lotacoes"] = "sigov.lotacao", ["vinculos"] = "sigov.vinculo",
        ["folhas"] = "sigov.folha", ["folha-eventos"] = "sigov.folha_evento", ["folha-lancamentos"] = "sigov.folha_lancamento",
        ["pontos"] = "sigov.ponto", ["ferias"] = "sigov.ferias", ["afastamentos"] = "sigov.afastamento", ["saude-ocupacional"] = "sigov.saude_ocupacional",
        ["esocial"] = "sigov.esocial", ["portal-usuarios"] = "sigov.portal_usuario", ["portal-acessos"] = "sigov.portal_acesso", ["eventos"] = "sigov.rh_evento"
    };

    public RhRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<RhRegistroResponse>> ListarAsync(long tenantId, string recurso, RhFiltro filtro, CancellationToken ct)
    {
        var table = Table(recurso);
        var page = Math.Max(1, filtro.Page);
        var pageSize = Math.Clamp(filtro.PageSize, 1, 100);
        var where = new StringBuilder("tenant_id = @TenantId and is_deleted = false");
        if (filtro.Ativo.HasValue) where.Append(" and ativo = @Ativo");
        if (!string.IsNullOrWhiteSpace(filtro.Termo)) where.Append(" and dados::text ilike @Termo");
        var parameters = new { TenantId = tenantId, filtro.Ativo, Termo = $"%{filtro.Termo}%", Limit = pageSize, Offset = (page - 1) * pageSize };
        using var cn = _context.CreateConnection();
        var total = await cn.ExecuteScalarAsync<long>(Command($"select count(1) from {table} where {where};", parameters, ct)).ConfigureAwait(false);
        var rows = await cn.QueryAsync<Row>(Command($"select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from {table} where {where} order by id desc limit @Limit offset @Offset;", parameters, ct)).ConfigureAwait(false);
        return new PagedResult<RhRegistroResponse>(rows.Select(r => ToResponse(recurso, r)).ToArray(), page, pageSize, total);
    }

    public async Task<RhRegistroResponse?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        var table = Table(recurso);
        using var cn = _context.CreateConnection();
        var row = await cn.QuerySingleOrDefaultAsync<Row>(Command($"select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from {table} where tenant_id = @TenantId and id = @Id and is_deleted = false;", new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false);
        return row is null ? null : ToResponse(recurso, row);
    }

    public async Task<long> CriarAsync(long tenantId, string recurso, RhRegistroCreateRequest request, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        var json = JsonSerializer.Serialize(request.Dados ?? new(), JsonOptions);
        using var cn = _context.CreateConnection();
        return await cn.ExecuteScalarAsync<long>(Command($"insert into {table} (tenant_id, dados, created_by) values (@TenantId, cast(@Dados as jsonb), @UsuarioId) returning id;", new { TenantId = tenantId, Dados = json, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task AtualizarAsync(long tenantId, string recurso, long id, RhRegistroUpdateRequest request, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        var json = JsonSerializer.Serialize(request.Dados ?? new(), JsonOptions);
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command($"update {table} set dados = cast(@Dados as jsonb), ativo = @Ativo, updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId and id = @Id and is_deleted = false;", new { TenantId = tenantId, Id = id, Dados = json, request.Ativo, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task ExcluirAsync(long tenantId, string recurso, long id, long? usuarioId, CancellationToken ct)
    {
        var table = Table(recurso);
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command($"update {table} set is_deleted = true, ativo = false, deleted_by = @UsuarioId, deleted_at = now(), updated_by = @UsuarioId, updated_at = now() where tenant_id = @TenantId and id = @Id;", new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<RhDashboardResponse> DashboardAsync(long tenantId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        const string sql = @"
        select
          (select count(1) from sigov.servidor where tenant_id=@TenantId and ativo=true and is_deleted=false) as ServidoresAtivos,
          (select count(1) from sigov.vinculo where tenant_id=@TenantId and ativo=true and is_deleted=false) as VinculosAtivos,
          (select count(1) from sigov.folha where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','Aberta')='Aberta') as FolhasAbertas,
          (select count(1) from sigov.ferias where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','Programada') in ('Programada','Aprovada')) as FeriasProgramadas,
          (select count(1) from sigov.afastamento where tenant_id=@TenantId and is_deleted=false and coalesce(dados->>'status','EmCurso') in ('Aprovado','EmCurso')) as AfastamentosAtivos,
          (select coalesce(sum((dados->>'valor')::numeric),0) from sigov.folha_lancamento where tenant_id=@TenantId and is_deleted=false) as TotalFolhaMes;
        ";
        return await cn.QuerySingleAsync<RhDashboardResponse>(Command(sql, new { TenantId = tenantId }, ct)).ConfigureAwait(false);
    }

    public async Task<RhPortalResumoResponse?> PortalServidorAsync(long tenantId, long servidorId, CancellationToken ct)
    {
        using var cn = _context.CreateConnection();
        var servidor = await ObterAsync(tenantId, "servidores", servidorId, ct).ConfigureAwait(false);
        if (servidor is null) return null;
        var nome = servidor.Dados.TryGetValue("nome", out var n) ? Convert.ToString(n) ?? "Servidor" : "Servidor";
        var contracheques = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.folha_lancamento where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc limit 24;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        var ferias = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.ferias where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        var afastamentos = await cn.QueryAsync<Row>(Command("select id, dados::text as dados, ativo, created_at as CreatedAt, updated_at as UpdatedAt from sigov.afastamento where tenant_id=@TenantId and is_deleted=false and (dados->>'servidorId')::bigint=@ServidorId order by id desc;", new { TenantId = tenantId, ServidorId = servidorId }, ct)).ConfigureAwait(false);
        return new RhPortalResumoResponse(servidorId, nome, contracheques.Select(r => ToResponse("folha-lancamentos", r)).ToArray(), ferias.Select(r => ToResponse("ferias", r)).ToArray(), afastamentos.Select(r => ToResponse("afastamentos", r)).ToArray());
    }

    public async Task<long> PrepararIntegracaoFinanceiraAsync(long tenantId, RhFinanceiroIntegracaoRequest request, long? usuarioId, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { tipo = "folha.financeiro.integracao.solicitada", request.FolhaId, request.DataCompetencia, request.NaturezaDespesaId, request.FonteRecursoId, request.Historico }, JsonOptions);
        using var cn = _context.CreateConnection();
        return await cn.ExecuteScalarAsync<long>(Command("insert into sigov.rh_evento (tenant_id, dados, created_by) values (@TenantId, cast(@Dados as jsonb), @UsuarioId) returning id;", new { TenantId = tenantId, Dados = payload, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<byte[]> ExportarAsync(long tenantId, string recurso, string formato, CancellationToken ct)
    {
        var all = await ListarAsync(tenantId, recurso, new RhFiltro(1, 100), ct).ConfigureAwait(false);
        if (formato.Equals("json", StringComparison.OrdinalIgnoreCase)) return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(all.Items, JsonOptions));
        var sb = new StringBuilder("id;recurso;ativo;dados\n");
        foreach (var item in all.Items) sb.Append(item.Id).Append(';').Append(item.Recurso).Append(';').Append(item.Ativo).Append(';').Append(JsonSerializer.Serialize(item.Dados, JsonOptions).Replace(';', ',')).AppendLine();
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Table(string recurso) => Tabelas.TryGetValue(recurso, out var table) ? table : throw new InvalidOperationException("Recurso de RH inválido.");
    private static RhRegistroResponse ToResponse(string recurso, Row row)
    {
        var dados = JsonSerializer.Deserialize<Dictionary<string, object?>>(row.Dados ?? "{}", JsonOptions) ?? new();
        if (recurso.Equals("servidores", StringComparison.OrdinalIgnoreCase))
        {
            Mask(dados, "cpf");
            Mask(dados, "documento");
            dados["classificacaoLgpd"] = "dados_pessoais_sensiveis";
        }

        return new RhRegistroResponse(row.Id, recurso, dados, row.Ativo, row.CreatedAt, row.UpdatedAt);
    }

    private static void Mask(IDictionary<string, object?> dados, string key)
    {
        if (!dados.TryGetValue(key, out var value) || value is null) return;
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text)) return;
        dados[key] = text.Length <= 4 ? "***" : new string('*', Math.Max(0, text.Length - 4)) + text[^4..];
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string? Dados { get; init; }
        public bool Ativo { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
