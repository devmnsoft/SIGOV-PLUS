using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Sigov.Application.Agro.Geo;
using Xunit;

namespace Sigov.UnitTests.Agro;

public sealed class CoordenadaValidatorTests
{
    private readonly CoordenadaValidator _validator = new(NullLogger<CoordenadaValidator>.Instance);

    [Fact]
    public void Latitude_Valida_Deve_Passar() => _validator.ValidarLatitude(-15.75m).IsSuccess.Should().BeTrue();

    [Fact]
    public void Latitude_Menor_Que_Menos_90_Deve_Falhar() => _validator.ValidarLatitude(-90.00000001m).IsFailure.Should().BeTrue();

    [Fact]
    public void Latitude_Maior_Que_90_Deve_Falhar() => _validator.ValidarLatitude(90.00000001m).IsFailure.Should().BeTrue();

    [Fact]
    public void Longitude_Valida_Deve_Passar() => _validator.ValidarLongitude(-48.10m).IsSuccess.Should().BeTrue();

    [Fact]
    public void Longitude_Menor_Que_Menos_180_Deve_Falhar() => _validator.ValidarLongitude(-180.00000001m).IsFailure.Should().BeTrue();

    [Fact]
    public void Longitude_Maior_Que_180_Deve_Falhar() => _validator.ValidarLongitude(180.00000001m).IsFailure.Should().BeTrue();
}
