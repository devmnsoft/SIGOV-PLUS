using Sigov.Domain.Common;
using Sigov.Domain.Saas.WhiteLabel;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantBrandingValidator
{
    public Result Validate(long tenantId, TenantBrandingUpdateRequest request, bool planoPermiteWhiteLabel)
    {
        if (request.LogoWidthPx is < 32 or > 1200 || request.LogoHeightPx is < 16 or > 600) return Result.Failure("As dimensões da logo estão fora dos limites permitidos.");
        if (request.LogoFit is not ("contain" or "cover" or "fill")) return Result.Failure("Modo de encaixe da logo inválido.");
        if (!TenantBranding.IsValidColor(request.CorPrimaria) || !TenantBranding.IsValidColor(request.CorSecundaria) || !TenantBranding.IsValidColor(request.CorAcento)) return Result.Failure("Cores devem estar no formato hexadecimal #RRGGBB.");
        Enum.TryParse<WhiteLabelTema>(request.Tema, true, out var tema);
        return new TenantBranding(0, tenantId, request.NomeExibicao, request.WhiteLabelAtivo, tema, request.CssCustomizado).Validate(planoPermiteWhiteLabel);
    }
}
