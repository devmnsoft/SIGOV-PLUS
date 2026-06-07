using Microsoft.Extensions.Options;

namespace Sigov.Web.Branding;

public sealed class SigovBrandProvider : ISigovBrandProvider
{
    private readonly SigovBrandOptions _options;

    public SigovBrandProvider(IOptions<SigovBrandOptions> options) => _options = options.Value;

    public SigovBrandOptions GetBrand() => _options;
}
