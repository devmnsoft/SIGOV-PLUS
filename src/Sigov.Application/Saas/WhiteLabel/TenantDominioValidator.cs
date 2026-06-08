using Sigov.Domain.Common;
using Sigov.Domain.Saas.WhiteLabel;

namespace Sigov.Application.Saas.WhiteLabel;

public sealed class TenantDominioValidator
{
    public Result Validate(long tenantId, TenantDominioCreateRequest request, bool planoPermiteDominio) => new TenantDominio(0, tenantId, request.Dominio, TenantDominioStatus.Solicitado).Validate(planoPermiteDominio);
}
