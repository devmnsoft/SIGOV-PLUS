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
}
