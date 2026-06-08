using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class PropriedadeRural : AggregateRoot
{
    public PropriedadeRural(long tenantId, long entidadeId, long produtorId, string codigoPropriedade, string nome, decimal? areaTotalHa, decimal? areaProdutivaHa, decimal? latitude, decimal? longitude, string? geoJson, string situacao)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (produtorId <= 0) throw new ArgumentException("Propriedade exige produtor.", nameof(produtorId));
        if (string.IsNullOrWhiteSpace(codigoPropriedade)) throw new ArgumentException("Código da propriedade é obrigatório.", nameof(codigoPropriedade));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Propriedade exige nome.", nameof(nome));
        if (areaTotalHa < 0) throw new ArgumentException("Área total não pode ser negativa.", nameof(areaTotalHa));
        if (areaProdutivaHa < 0) throw new ArgumentException("Área produtiva não pode ser negativa.", nameof(areaProdutivaHa));
        if (areaTotalHa.HasValue && areaProdutivaHa.HasValue && areaProdutivaHa > areaTotalHa) throw new ArgumentException("Área produtiva não pode ser maior que área total.", nameof(areaProdutivaHa));
        ValidateCoordinates(latitude, longitude); ValidateGeoJsonText(geoJson);
        TenantId = tenantId; EntidadeId = entidadeId; ProdutorId = produtorId; CodigoPropriedade = codigoPropriedade.Trim(); Nome = nome.Trim(); AreaTotalHa = areaTotalHa; AreaProdutivaHa = areaProdutivaHa; Latitude = latitude; Longitude = longitude; GeoJson = geoJson; Situacao = string.IsNullOrWhiteSpace(situacao) ? throw new ArgumentException("Situação da propriedade é obrigatória.", nameof(situacao)) : situacao.Trim();
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long ProdutorId { get; }
    public string CodigoPropriedade { get; }
    public string Nome { get; }
    public decimal? AreaTotalHa { get; }
    public decimal? AreaProdutivaHa { get; }
    public decimal? Latitude { get; }
    public decimal? Longitude { get; }
    public string? GeoJson { get; }
    public string Situacao { get; }
    internal static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude is < -90m or > 90m) throw new ArgumentException("Latitude deve estar entre -90 e 90.", nameof(latitude));
        if (longitude is < -180m or > 180m) throw new ArgumentException("Longitude deve estar entre -180 e 180.", nameof(longitude));
        if (latitude.HasValue != longitude.HasValue) throw new ArgumentException("Latitude e longitude devem ser informadas em conjunto.");
    }
    internal static void ValidateGeoJsonText(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return;
        if (!geoJson.TrimStart().StartsWith('{')) throw new ArgumentException("GeoJSON inválido.", nameof(geoJson));
    }
}
