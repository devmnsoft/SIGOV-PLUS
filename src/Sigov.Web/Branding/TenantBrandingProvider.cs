namespace Sigov.Web.Branding;

public sealed class TenantBrandingProvider : ITenantBrandingProvider
{
    private readonly ISigovBrandProvider _fallback;

    public TenantBrandingProvider(ISigovBrandProvider fallback) => _fallback = fallback;

    public TenantBrandingViewModel GetBranding()
    {
        var brand = _fallback.GetBrand();
        return new TenantBrandingViewModel(brand.ProductName, brand.LogoPath, "#0d6efd", "#6c757d", "#198754", "SIGOV", brand.FaviconPath, null, false);
    }
}
