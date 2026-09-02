namespace Sigov.Application.Saas.WhiteLabel;

public sealed record TenantBrandingResponse(
    long Id,
    long TenantId,
    string NomeExibicao,
    string? LogoUrl,
    string? CorPrimaria,
    string? CorSecundaria,
    string? CorAcento,
    string Tema,
    string? FaviconUrl,
    string? CssCustomizado,
    bool WhiteLabelAtivo,
    bool PlanoPermiteWhiteLabel,
    int LogoWidthPx = 220,
    int LogoHeightPx = 72,
    string LogoFit = "contain",
    string? LogoOriginalFilename = null,
    string? LogoContentType = null,
    long? LogoSizeBytes = null,
    DateTimeOffset? LogoUploadedAt = null);

public sealed record TenantBrandingUpdateRequest(
    string NomeExibicao,
    string? LogoUrl,
    string? LogoStorageKey,
    string? CorPrimaria,
    string? CorSecundaria,
    string? CorAcento,
    string Tema,
    string? FaviconUrl,
    string? CssCustomizado,
    bool WhiteLabelAtivo,
    int LogoWidthPx = 220,
    int LogoHeightPx = 72,
    string LogoFit = "contain",
    string? LogoOriginalFilename = null,
    string? LogoContentType = null,
    long? LogoSizeBytes = null);

public sealed record TenantDominioCreateRequest(string Dominio);
public sealed record TenantDominioResponse(long Id, long TenantId, string Dominio, string Status, bool Verificado, string? TokenVerificacao, string? SslStatus);
public sealed record VerificarDominioRequest(string TokenInformado);
