using System.Text.Json;

namespace Sigov.Domain.Agro.Geo;

public sealed class GeoJsonDocument
{
    private static readonly ISet<string> AllowedTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Point",
        "LineString",
        "Polygon",
        "MultiPolygon",
        "Feature",
        "FeatureCollection"
    };

    public GeoJsonDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("GeoJSON é obrigatório.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("GeoJSON deve informar type.", nameof(json));
        }

        var type = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(type) || !AllowedTypes.Contains(type))
        {
            throw new ArgumentException("Tipo GeoJSON não permitido.", nameof(json));
        }

        Type = type;
        Json = json;
    }

    public string Type { get; }
    public string Json { get; }

    public static bool IsAllowedType(string? type) => !string.IsNullOrWhiteSpace(type) && AllowedTypes.Contains(type);
}
