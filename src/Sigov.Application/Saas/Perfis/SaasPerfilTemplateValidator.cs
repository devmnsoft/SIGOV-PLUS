using Sigov.Domain.Common;
using Sigov.Domain.Saas.Perfis;

namespace Sigov.Application.Saas.Perfis;

public sealed class SaasPerfilTemplateValidator
{
    public Result Validate(SaasPerfilTemplateResponse request) => new SaasPerfilTemplate(request.Id, request.Codigo, request.Nome, request.NivelBase).Validate();
}
