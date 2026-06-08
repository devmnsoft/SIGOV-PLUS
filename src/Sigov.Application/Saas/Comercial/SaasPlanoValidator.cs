using Sigov.Domain.Common;
using Sigov.Domain.Saas.Comercial;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasPlanoValidator
{
    public Result ValidateCreate(SaasPlanoCreateRequest request)
    {
        Enum.TryParse<SaasPlanoTipo>(NormalizeEnum(request.TipoPlano), true, out var tipo);
        Enum.TryParse<SaasPeriodicidade>(NormalizeEnum(request.Periodicidade), true, out var periodicidade);
        var plano = new SaasPlano(0, request.Codigo, request.Nome, request.Descricao, request.Publico, tipo, request.PrecoBase, periodicidade, request.LimiteUsuarios, request.PermiteWhiteLabel, request.PermiteDominioCustomizado);
        return plano.Validate();
    }

    public Result ValidateUpdate(SaasPlanoUpdateRequest request)
    {
        var plano = new SaasPlano(0, "UPDATE", request.Nome, request.Descricao, request.Publico, SaasPlanoTipo.Publico, request.PrecoBase, SaasPeriodicidade.Mensal, request.LimiteUsuarios, request.PermiteWhiteLabel, request.PermiteDominioCustomizado);
        return plano.Validate();
    }

    internal static string NormalizeEnum(string value) => value.Replace("_", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
}
