using Microsoft.AspNetCore.Razor.TagHelpers;
using Sigov.Web.Services.Visual;

namespace Sigov.Web.TagHelpers;

[HtmlTargetElement("sigov-image")]
public sealed class SigovImageTagHelper(IVisualAssetProvider assets) : TagHelper
{
    [HtmlAttributeName("asset")] public required string Asset { get; set; }
    [HtmlAttributeName("alt")] public required string Alt { get; set; }
    [HtmlAttributeName("loading")] public string Loading { get; set; } = "lazy";
    [HtmlAttributeName("priority")] public string Priority { get; set; } = "auto";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!assets.TryResolve(Asset, out var descriptor)) throw new InvalidOperationException($"Asset visual SIGOV não registrado: {Asset}");
        output.TagName = "img";
        output.Attributes.SetAttribute("src", descriptor.Path);
        output.Attributes.SetAttribute("alt", Alt);
        output.Attributes.SetAttribute("width", descriptor.Width);
        output.Attributes.SetAttribute("height", descriptor.Height);
        output.Attributes.SetAttribute("loading", Loading == "eager" ? "eager" : "lazy");
        output.Attributes.SetAttribute("fetchpriority", Priority == "high" ? "high" : "auto");
        output.Attributes.SetAttribute("decoding", "async");
        output.Attributes.SetAttribute("data-sigov-image-fallback", descriptor.FallbackPath);
    }
}
