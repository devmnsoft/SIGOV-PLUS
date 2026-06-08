using Sigov.Application.Agro.Geo;
using Sigov.Domain.Agro;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Propriedades;

public sealed class AgroPropriedadeValidator
{
    private readonly ICoordenadaValidator _coordenada; private readonly IGeoJsonValidator _geoJson;
    public AgroPropriedadeValidator(ICoordenadaValidator coordenada, IGeoJsonValidator geoJson) { _coordenada = coordenada; _geoJson = geoJson; }
    public Result Validar(AgroPropriedadeCreateRequest r) => Validar(r.ProdutorId, r.CodigoPropriedade ?? "AUTO", r.Nome, r.AreaTotalHa, r.AreaProdutivaHa, r.Latitude, r.Longitude, r.GeoJson, r.Situacao);
    public Result Validar(AgroPropriedadeUpdateRequest r) => Validar(r.ProdutorId, r.CodigoPropriedade, r.Nome, r.AreaTotalHa, r.AreaProdutivaHa, r.Latitude, r.Longitude, r.GeoJson, r.Situacao);
    private Result Validar(long produtorId, string codigo, string nome, decimal? total, decimal? produtiva, decimal? lat, decimal? lon, string? geoJson, string situacao)
    { try { _ = new PropriedadeRural(1, 1, produtorId, codigo, nome, total, produtiva, lat, lon, geoJson, situacao); } catch (ArgumentException ex) { return Result.Failure(ex.Message); } var c = _coordenada.Validar(lat, lon); if (c.IsFailure) return c; return _geoJson.Validar(geoJson); }
}
public sealed class AgroTalhaoValidator
{
    private readonly ICoordenadaValidator _coordenada; private readonly IGeoJsonValidator _geoJson;
    public AgroTalhaoValidator(ICoordenadaValidator coordenada, IGeoJsonValidator geoJson) { _coordenada = coordenada; _geoJson = geoJson; }
    public Result Validar(AgroTalhaoCreateRequest r) { try { _ = new Talhao(1, 1, r.PropriedadeId, r.Codigo, r.Nome, r.AreaHa, r.Latitude, r.Longitude, r.Situacao); } catch (ArgumentException ex) { return Result.Failure(ex.Message); } var c = _coordenada.Validar(r.Latitude, r.Longitude); if (c.IsFailure) return c; return _geoJson.Validar(r.GeoJson); }
}
public sealed class AgroCulturaValidator { public Result Validar(AgroCulturaCreateRequest r) { try { _ = new Cultura(1, 1, r.Codigo, r.Nome, r.TipoCultura, r.CicloDias, r.UnidadeMedida); return Result.Success(); } catch (ArgumentException ex) { return Result.Failure(ex.Message); } } }
public sealed class AgroSafraValidator { public Result Validar(AgroSafraCreateRequest r) { try { _ = new Safra(1, 1, r.ExercicioId, r.Codigo, r.Nome, r.AnoInicio, r.AnoFim, r.DataInicio, r.DataFim, r.Status); return Result.Success(); } catch (ArgumentException ex) { return Result.Failure(ex.Message); } } }
