using System.Text.Json;
using Dapper;
using Sigov.Application.Saas.WhiteLabel;
using Sigov.Domain.Saas.WhiteLabel;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Saas.WhiteLabel;

public sealed class TenantBrandingRepository : ITenantBrandingRepository
{
    private readonly DapperContext _context;
    public TenantBrandingRepository(DapperContext context) => _context = context;

    public async Task<TenantBrandingResponse?> GetAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"select b.id as Id,b.tenant_id as TenantId,b.nome_exibicao as NomeExibicao,b.logo_url as LogoUrl,b.cor_primaria as CorPrimaria,b.cor_secundaria as CorSecundaria,b.cor_acento as CorAcento,b.tema as Tema,b.favicon_url as FaviconUrl,b.css_customizado as CssCustomizado,b.white_label_ativo as WhiteLabelAtivo,b.logo_width_px as LogoWidthPx,b.logo_height_px as LogoHeightPx,b.logo_fit as LogoFit,b.logo_original_filename as LogoOriginalFilename,b.logo_content_type as LogoContentType,b.logo_size_bytes as LogoSizeBytes,b.logo_uploaded_at as LogoUploadedAt,
exists(select 1 from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id where a.tenant_id=b.tenant_id and (p.permite_white_label=true or exists(select 1 from sigov.saas_assinatura_addon aa where aa.tenant_id=a.tenant_id and aa.assinatura_id=a.id and aa.addon_codigo='WHITE_LABEL' and aa.status='ATIVO'))) as PlanoPermiteWhiteLabel
from sigov.saas_tenant_branding b where b.tenant_id=@TenantId limit 1;
";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantBrandingResponse>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> PlanoPermiteWhiteLabelAsync(long tenantId, CancellationToken cancellationToken)
    {
        const string sql = "select exists(select 1 from sigov.saas_assinatura a join sigov.saas_plano p on p.id=a.plano_id where a.tenant_id=@TenantId and (p.permite_white_label=true or exists(select 1 from sigov.saas_assinatura_addon aa where aa.tenant_id=a.tenant_id and aa.assinatura_id=a.id and aa.addon_codigo='WHITE_LABEL' and aa.status='ATIVO')));";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<TenantBrandingResponse> UpsertAsync(long tenantId, TenantBrandingUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = @"insert into sigov.saas_tenant_branding (tenant_id,nome_exibicao,logo_url,logo_storage_key,cor_primaria,cor_secundaria,cor_acento,tema,favicon_url,css_customizado,white_label_ativo,logo_width_px,logo_height_px,logo_fit,logo_original_filename,logo_content_type,logo_size_bytes,logo_uploaded_at,created_by,updated_by)
values (@TenantId,@NomeExibicao,@LogoUrl,@LogoStorageKey,@CorPrimaria,@CorSecundaria,@CorAcento,@Tema,@FaviconUrl,@CssCustomizado,@WhiteLabelAtivo,@LogoWidthPx,@LogoHeightPx,@LogoFit,@LogoOriginalFilename,@LogoContentType,@LogoSizeBytes,@LogoUploadedAt,@UsuarioId,@UsuarioId)
on conflict (tenant_id) do update set nome_exibicao=excluded.nome_exibicao, logo_url=excluded.logo_url, logo_storage_key=excluded.logo_storage_key, cor_primaria=excluded.cor_primaria, cor_secundaria=excluded.cor_secundaria, cor_acento=excluded.cor_acento, tema=excluded.tema, favicon_url=excluded.favicon_url, css_customizado=excluded.css_customizado, white_label_ativo=excluded.white_label_ativo, logo_width_px=excluded.logo_width_px, logo_height_px=excluded.logo_height_px, logo_fit=excluded.logo_fit, logo_original_filename=coalesce(excluded.logo_original_filename,sigov.saas_tenant_branding.logo_original_filename), logo_content_type=coalesce(excluded.logo_content_type,sigov.saas_tenant_branding.logo_content_type), logo_size_bytes=coalesce(excluded.logo_size_bytes,sigov.saas_tenant_branding.logo_size_bytes), logo_uploaded_at=coalesce(excluded.logo_uploaded_at,sigov.saas_tenant_branding.logo_uploaded_at), updated_by=@UsuarioId, updated_at=now();
";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, request.NomeExibicao, request.LogoUrl, request.LogoStorageKey, request.CorPrimaria, request.CorSecundaria, request.CorAcento, Tema = request.Tema.ToUpperInvariant(), request.FaviconUrl, CssCustomizado = TenantBranding.SanitizeCss(request.CssCustomizado), request.WhiteLabelAtivo, request.LogoWidthPx, request.LogoHeightPx, request.LogoFit, request.LogoOriginalFilename, request.LogoContentType, request.LogoSizeBytes, request.LogoUploadedAt, UsuarioId = usuarioId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (await GetAsync(tenantId, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken)
    {
        const string sql = "insert into sigov.saas_evento (tenant_id,tipo_evento,origem,origem_id,payload,correlation_id) values (@TenantId,@TipoEvento,@Origem,@OrigemId,cast(@Payload as jsonb),@CorrelationId);";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TipoEvento = tipoEvento, Origem = origem, OrigemId = origemId, Payload = JsonSerializer.Serialize(payload), CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
