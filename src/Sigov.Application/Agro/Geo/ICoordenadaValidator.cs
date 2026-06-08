using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public interface ICoordenadaValidator
{
    Result ValidarLatitude(decimal? latitude);
    Result ValidarLongitude(decimal? longitude);
    Result Validar(decimal? latitude, decimal? longitude);
}
