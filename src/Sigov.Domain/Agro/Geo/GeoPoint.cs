namespace Sigov.Domain.Agro.Geo;

public sealed record GeoPoint
{
    public GeoPoint(decimal latitude, decimal longitude)
    {
        if (latitude is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude deve estar entre -90 e 90.");
        }

        if (longitude is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude deve estar entre -180 e 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }
    public decimal Longitude { get; }
}
