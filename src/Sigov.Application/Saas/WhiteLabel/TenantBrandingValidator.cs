using Sigov.Domain.Common;
using Sigov.Domain.Saas.WhiteLabel;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantBrandingValidator
{
    public Result Validate(long tenantId, TenantBrandingUpdateRequest request, bool planoPermiteWhiteLabel)
    {
        if (!TenantBranding.IsValidColor(request.CorPrimaria) || !TenantBranding.IsValidColor(request.CorSecundaria) || !TenantBranding.IsValidColor(request.CorAcento)) return Result.Failure("Cores devem estar no formato hexadecimal #RRGGBB.");
        Enum.TryParse<WhiteLabelTema>(request.Tema, true, out var tema);
        return new TenantBranding(0, tenantId, request.NomeExibicao, request.WhiteLabelAtivo, tema, request.CssCustomizado).Validate(planoPermiteWhiteLabel);
    }
}
