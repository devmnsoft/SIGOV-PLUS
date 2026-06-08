using Microsoft.Extensions.Logging;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public sealed class CoordenadaValidator : ICoordenadaValidator
{
    private readonly ILogger<CoordenadaValidator> _logger;

    public CoordenadaValidator(ILogger<CoordenadaValidator> logger) => _logger = logger;

    public Result ValidarLatitude(decimal? latitude)
    {
        if (latitude is null)
        {
            return Result.Success();
        }

        if (latitude is < -90m or > 90m)
        {
            _logger.LogWarning("Latitude inválida informada para georreferenciamento Agro.");
            return Result.Failure("Latitude deve estar entre -90 e 90.");
        }

        return Result.Success();
    }

    public Result ValidarLongitude(decimal? longitude)
    {
        if (longitude is null)
        {
            return Result.Success();
        }

        if (longitude is < -180m or > 180m)
        {
            _logger.LogWarning("Longitude inválida informada para georreferenciamento Agro.");
            return Result.Failure("Longitude deve estar entre -180 e 180.");
        }

        return Result.Success();
    }

    public Result Validar(decimal? latitude, decimal? longitude)
    {
        var latitudeResult = ValidarLatitude(latitude);
        if (latitudeResult.IsFailure)
        {
            return latitudeResult;
        }

        var longitudeResult = ValidarLongitude(longitude);
        if (longitudeResult.IsFailure)
        {
            return longitudeResult;
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            return Result.Failure("Latitude e longitude devem ser informadas em conjunto.");
        }

        return Result.Success();
    }
}
