using Microsoft.AspNetCore.Razor.TagHelpers;
using Sigov.Web.Services.Visual;

namespace Sigov.Web.TagHelpers;

[HtmlTargetElement("sigov-icon-button")]
public sealed class SigovIconButtonTagHelper(IIconRegistry registry) : TagHelper
{
    [HtmlAttributeName("icon")] public required string Icon { get; set; }
    [HtmlAttributeName("label")] public required string Label { get; set; }
    [HtmlAttributeName("variant")] public string Variant { get; set; } = "ghost";
    [HtmlAttributeName("action")] public string? Action { get; set; }
    [HtmlAttributeName("tooltip")] public string? Tooltip { get; set; }
    [HtmlAttributeName("disabled")] public bool Disabled { get; set; }
    [HtmlAttributeName("loading")] public bool Loading { get; set; }
    [HtmlAttributeName("badge")] public int? Badge { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!registry.TryGet(Icon, out var definition)) throw new InvalidOperationException($"Ícone SIGOV não registrado: {Icon}");
        output.TagName = "button";
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", $"sigov-icon-btn sigov-icon-btn--{Variant}");
        output.Attributes.SetAttribute("aria-label", Label);
        output.Attributes.SetAttribute("title", Tooltip ?? Label);
        if (!string.IsNullOrWhiteSpace(Action)) output.Attributes.SetAttribute($"data-sigov-{Action}", string.Empty);
        if (Disabled || Loading) output.Attributes.SetAttribute("disabled", "disabled");
        if (Loading) output.Attributes.SetAttribute("aria-busy", "true");
        var badge = Badge is > 0 ? $"<span class=\"sigov-icon-btn__badge\">{Badge}</span>" : string.Empty;
        output.Content.SetHtmlContent($"<svg class=\"sigov-icon sigov-icon--20\" width=\"20\" height=\"20\" aria-hidden=\"true\" focusable=\"false\"><use href=\"/icons/sigov-icons.svg#{definition.SymbolId}\"></use></svg>{badge}");
    }
}
