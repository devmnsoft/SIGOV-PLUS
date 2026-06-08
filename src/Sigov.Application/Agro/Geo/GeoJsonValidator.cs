using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sigov.Domain.Agro.Geo;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public sealed class GeoJsonValidator : IGeoJsonValidator
{
    private readonly ILogger<GeoJsonValidator> _logger;

    public GeoJsonValidator(ILogger<GeoJsonValidator> logger) => _logger = logger;

    public Result Validar(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson))
        {
            return Result.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure("GeoJSON deve ser um objeto JSON.");
            }

            if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                return Result.Failure("GeoJSON deve informar type.");
            }

            var type = typeElement.GetString();
            if (!GeoJsonDocument.IsAllowedType(type))
            {
                return Result.Failure("Tipo GeoJSON não permitido.");
            }

            if (type is "Feature")
            {
                return root.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Object
                    ? Result.Success()
                    : Result.Failure("GeoJSON Feature deve informar geometry.");
            }

            if (type is "FeatureCollection")
            {
                return root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array
                    ? Result.Success()
                    : Result.Failure("GeoJSON FeatureCollection deve informar features.");
            }

            return root.TryGetProperty("coordinates", out var coordinates) && coordinates.ValueKind == JsonValueKind.Array
                ? Result.Success()
                : Result.Failure("GeoJSON deve informar coordinates.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "GeoJSON inválido informado para Agro.");
            return Result.Failure("GeoJSON inválido.");
        }
    }
}
