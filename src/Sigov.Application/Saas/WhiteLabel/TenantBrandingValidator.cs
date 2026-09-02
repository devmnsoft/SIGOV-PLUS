using Sigov.Domain.Common;
using Sigov.Domain.Saas.WhiteLabel;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantBrandingValidator
{
    private static readonly HashSet<string> LogoFits = new(StringComparer.OrdinalIgnoreCase) { "contain", "cover", "fill" };
    private static readonly HashSet<string> LogoContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp" };

    public Result Validate(long tenantId, TenantBrandingUpdateRequest request, bool planoPermiteWhiteLabel)
    {
        if (!TenantBranding.IsValidColor(request.CorPrimaria) || !TenantBranding.IsValidColor(request.CorSecundaria) || !TenantBranding.IsValidColor(request.CorAcento))
            return Result.Failure("Cores devem estar no formato hexadecimal #RRGGBB.");

        if (request.LogoWidthPx is < 80 or > 480)
            return Result.Failure("A largura da logo deve ficar entre 80 e 480 px.");

        if (request.LogoHeightPx is < 32 or > 180)
            return Result.Failure("A altura da logo deve ficar entre 32 e 180 px.");

        if (!LogoFits.Contains(request.LogoFit ?? string.Empty))
            return Result.Failure("O encaixe da logo deve ser contain, cover ou fill.");

        if (!string.IsNullOrWhiteSpace(request.LogoUrl) && !IsSafeLogoUrl(request.LogoUrl))
            return Result.Failure("A URL da logo deve ser HTTPS, HTTP ou um arquivo enviado pela plataforma.");

        if (!string.IsNullOrWhiteSpace(request.LogoContentType) && !LogoContentTypes.Contains(request.LogoContentType))
            return Result.Failure("A logo deve estar em PNG, JPG ou WebP.");

        if (request.LogoSizeBytes is < 0 or > 2_097_152)
            return Result.Failure("A logo deve ter no maximo 2 MB.");

        Enum.TryParse<WhiteLabelTema>(request.Tema, true, out var tema);
        return new TenantBranding(0, tenantId, request.NomeExibicao, request.WhiteLabelAtivo, tema, request.CssCustomizado).Validate(planoPermiteWhiteLabel);
    }

    private static bool IsSafeLogoUrl(string logoUrl)
    {
        var value = logoUrl.Trim();
        if (value.StartsWith("/uploads/tenant-branding/", StringComparison.OrdinalIgnoreCase))
            return true;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(uri.Host);
    }
}
