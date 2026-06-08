using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public interface IGeoJsonValidator
{
    Result Validar(string? geoJson);
}
