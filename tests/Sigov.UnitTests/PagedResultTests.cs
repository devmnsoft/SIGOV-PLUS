using FluentAssertions;
using Sigov.Application.Common;
using Xunit;

namespace Sigov.UnitTests;

public sealed class PagedResultTests
{
    [Fact]
    public void Deve_Calcular_Total_De_Paginas_E_Navegacao()
    {
        var result = new PagedResult<int>(new[] { 1, 2 }, 2, 2, 5);

        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Deve_Manter_Total_Filtrado_Em_Todas_As_Paginas()
    {
        var primeira = new PagedResult<int>(Enumerable.Range(1, 10).ToArray(), 1, 10, 23);
        var intermediaria = new PagedResult<int>(Enumerable.Range(11, 10).ToArray(), 2, 10, 23);
        var ultima = new PagedResult<int>(Enumerable.Range(21, 3).ToArray(), 3, 10, 23);

        primeira.TotalItems.Should().Be(23);
        intermediaria.TotalItems.Should().Be(23);
        ultima.TotalItems.Should().Be(23);
        new[] { primeira.Items.Count, intermediaria.Items.Count, ultima.Items.Count }.Should().Equal(10, 10, 3);
        ultima.TotalPages.Should().Be(3);
        ultima.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Deve_Tratar_Resultados_Vazio_E_Pagina_Fora_Do_Intervalo()
    {
        var vazio = PagedResult<int>.Empty(1, 10);
        var foraDoIntervalo = new PagedResult<int>(Array.Empty<int>(), 4, 10, 23);

        vazio.TotalItems.Should().Be(0);
        vazio.TotalPages.Should().Be(0);
        foraDoIntervalo.TotalItems.Should().Be(23);
        foraDoIntervalo.Items.Should().BeEmpty();
        foraDoIntervalo.HasNextPage.Should().BeFalse();
    }
}
