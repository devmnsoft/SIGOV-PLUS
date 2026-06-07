namespace Sigov.Web.Branding;

public sealed class SigovBrandOptions
{
    public string ProductName { get; set; } = "sigov";

    public string Tagline { get; set; } = "Gestão pública municipal SaaS";

    public string LogoPath { get; set; } = "/img/sigov-logo.svg";

    public string LogoMarkPath { get; set; } = "/img/sigov-logo-mark.svg";

    public string FaviconPath { get; set; } = "/img/sigov-favicon.svg";
}
