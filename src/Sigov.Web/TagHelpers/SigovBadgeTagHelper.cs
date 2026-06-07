using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Sigov.Web.TagHelpers;

[HtmlTargetElement("sigov-badge")]
public sealed class SigovBadgeTagHelper : TagHelper
{
    public string Variant { get; set; } = "secondary";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.Attributes.SetAttribute("class", $"badge sigov-badge bg-{Variant}");
    }
}
