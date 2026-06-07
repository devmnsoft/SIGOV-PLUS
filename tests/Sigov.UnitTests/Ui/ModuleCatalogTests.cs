using FluentAssertions;
using Sigov.Application.Commercial;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void Catalogo_Deve_Conter_Modulos_Principais()
    {
        var service = new ModuleCatalogService();

        var modules = service.GetModules();

        modules.Should().Contain(module => module.Code == "core");
        modules.Should().Contain(module => module.Code == "financeiro");
        modules.Should().Contain(module => module.Code == "integracoes");
        modules.Should().HaveCountGreaterThanOrEqualTo(21);
    }
}
