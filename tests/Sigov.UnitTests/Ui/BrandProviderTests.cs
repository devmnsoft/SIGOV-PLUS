using FluentAssertions;
using Microsoft.Extensions.Options;
using Sigov.Web.Branding;
using Xunit;

namespace Sigov.UnitTests.Ui;

public sealed class BrandProviderTests
{
    [Fact]
    public void BrandProvider_Deve_Retornar_Nome_Sigov()
    {
        var provider = new SigovBrandProvider(Options.Create(new SigovBrandOptions()));

        provider.GetBrand().ProductName.Should().Be("sigov");
        provider.GetBrand().LogoPath.Should().Be("/img/sigov-logo.svg");
    }
}
