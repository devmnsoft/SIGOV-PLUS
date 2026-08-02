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
    [HtmlAttributeName("type")] public string Type { get; set; } = "button";
    [HtmlAttributeName("href")] public string? Href { get; set; }
    [HtmlAttributeName("text")] public string? Text { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!registry.TryGet(Icon, out var definition)) throw new InvalidOperationException($"Ícone SIGOV não registrado: {Icon}");
        var isLink = !string.IsNullOrWhiteSpace(Href);
        output.TagName = isLink ? "a" : "button";
        if (isLink) output.Attributes.SetAttribute("href", Href);
        else if (Type is "button" or "submit" or "reset") output.Attributes.SetAttribute("type", Type);
        else throw new InvalidOperationException($"Tipo de botão SIGOV inválido: {Type}");
        var suppliedClass = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.SetAttribute("class", string.Join(' ', new[] { "sigov-icon-btn", $"sigov-icon-btn--{Variant}", suppliedClass }.Where(value => !string.IsNullOrWhiteSpace(value))));
        output.Attributes.SetAttribute("aria-label", Label);
        output.Attributes.SetAttribute("title", Tooltip ?? Label);
        if (!string.IsNullOrWhiteSpace(Action)) output.Attributes.SetAttribute($"data-sigov-{Action}", string.Empty);
        if (Disabled || Loading) {
            if (isLink) { output.Attributes.SetAttribute("aria-disabled", "true"); output.Attributes.SetAttribute("tabindex", "-1"); }
            else output.Attributes.SetAttribute("disabled", "disabled");
        }
        if (Loading) output.Attributes.SetAttribute("aria-busy", "true");
        var badge = Badge is > 0 ? $"<span class=\"sigov-icon-btn__badge\">{Badge}</span>" : string.Empty;
        var text = string.IsNullOrWhiteSpace(Text) ? string.Empty : $"<span class=\"sigov-icon-btn__text\">{System.Net.WebUtility.HtmlEncode(Text)}</span>";
        output.Content.SetHtmlContent($"<svg class=\"sigov-icon sigov-icon--20\" width=\"20\" height=\"20\" aria-hidden=\"true\" focusable=\"false\"><use href=\"#{definition.SymbolId}\"></use></svg>{text}{badge}");
    }
}
