namespace Sigov.Web.Branding;

public sealed record TenantBrandingViewModel(string ProductName, string? LogoUrl, string PrimaryColor, string SecondaryColor, string AccentColor, string Theme, string? FaviconUrl, string? CustomCss, bool WhiteLabelActive);
