using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sigov.Application.Agro.Geo;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class GeoJsonValidatorTests
{
    private readonly GeoJsonValidator _validator = new(NullLogger<GeoJsonValidator>.Instance);

    [Fact]
    public void GeoJson_Point_Valido_Deve_Passar()
    {
        _validator.Validar("{\"type\":\"Point\",\"coordinates\":[-48.0,-15.0]}").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GeoJson_Sem_Type_Deve_Falhar()
    {
        _validator.Validar("{\"coordinates\":[-48.0,-15.0]}").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GeoJson_Sem_Coordinates_Deve_Falhar()
    {
        _validator.Validar("{\"type\":\"Point\"}").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Tipo_GeoJson_Nao_Permitido_Deve_Falhar()
    {
        _validator.Validar("{\"type\":\"GeometryCollection\",\"geometries\":[]}").IsFailure.Should().BeTrue();
    }
}
