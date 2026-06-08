using Sigov.Domain.Agro.Transparencia;

namespace Sigov.Application.Agro.Transparencia;

public sealed class AgroDatasetValidator
{
    public AgroDatasetPublico Validate(AgroDatasetPublicoCreateRequest request, long tenantId, long? entidadeId)
    {
        var tipo = Enum.TryParse<AgroDatasetTipo>(request.TipoDataset, true, out var parsed) ? parsed : AgroDatasetTipo.PRODUCAO_AGREGADA;
        return new AgroDatasetPublico(tenantId, entidadeId, request.Codigo, request.Nome, tipo, request.Anonimizado, request.Publico);
    }
}
